using System.Net;
using System.Text.Json;

namespace StudyFlow.Services.Yandex
{
    /// <summary>
    /// Calls the Yandex Disk REST API on behalf of the signed-in user.
    /// The app only has access to its own folder, Приложения/StudyFlow ("app:/" in API paths).
    /// </summary>
    public class YandexDiskClient
    {
        // Short API calls get their own timeout; the shared HttpClient has none because uploads can take long.
        private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);

        private readonly HttpClient _http;
        private readonly YandexAuthService _auth;

        public YandexDiskClient(HttpClient http, YandexAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        /// <summary>
        /// Returns the size of the user's Disk and how much of it is used.
        /// </summary>
        /// <returns>Disk info, or null if the user is not signed in.</returns>
        public async Task<YandexDiskInfo?> GetDiskInfoAsync(CancellationToken cancellationToken = default)
        {
            if (!_auth.IsSignedIn)
                return null;

            try
            {
                using var response = await SendAsync(HttpMethod.Get, string.Empty, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize(body, JsonContext.Default.YandexDiskInfo);
            }
            catch (YandexDiskException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
            {
                return null;
            }
        }

        public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await SendAsync(HttpMethod.Get, $"resources?path={Uri.EscapeDataString(path)}&fields=path", cancellationToken);
                return true;
            }
            catch (YandexDiskException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a folder; does nothing if it already exists. The parent folder must exist.
        /// </summary>
        public async Task CreateFolderAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await SendAsync(HttpMethod.Put, $"resources?path={Uri.EscapeDataString(path)}", cancellationToken);
            }
            catch (YandexDiskException e) when (e.StatusCode == HttpStatusCode.Conflict)
            {
                // 409: the folder is already there.
            }
        }

        /// <summary>
        /// Uploads a file, replacing the one at <paramref name="path"/> if there is one.
        /// </summary>
        public async Task UploadAsync(string path, Stream content, CancellationToken cancellationToken = default)
        {
            // Two steps: ask the API for a one-time upload URL, then PUT the bytes there (no OAuth header needed).
            string href;
            using (var response = await SendAsync(HttpMethod.Get, $"resources/upload?path={Uri.EscapeDataString(path)}&overwrite=true", cancellationToken))
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                href = json.RootElement.GetProperty("href").GetString()
                    ?? throw new YandexDiskException(HttpStatusCode.BadGateway, "No upload URL");
            }

            using var upload = await _http.PutAsync(href, new StreamContent(content), cancellationToken);
            if (!upload.IsSuccessStatusCode)
                throw YandexDiskException.FromStatus(upload.StatusCode, await upload.Content.ReadAsStringAsync(cancellationToken));
        }

        /// <summary>
        /// Moves or renames a file or folder. Never replaces an existing file at <paramref name="to"/>.
        /// </summary>
        /// <returns>False if there was nothing at <paramref name="from"/>.</returns>
        public async Task<bool> MoveAsync(string from, string to, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await SendAsync(HttpMethod.Post,
                    $"resources/move?from={Uri.EscapeDataString(from)}&path={Uri.EscapeDataString(to)}&overwrite=false", cancellationToken);
                return true;
            }
            catch (YandexDiskException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        /// <summary>
        /// Moves a file or folder to the Disk trash; does nothing if it is already gone.
        /// </summary>
        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await SendAsync(HttpMethod.Delete, $"resources?path={Uri.EscapeDataString(path)}&permanently=false", cancellationToken);
            }
            catch (YandexDiskException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUrl, CancellationToken cancellationToken)
        {
            var token = await _auth.GetAccessTokenAsync(cancellationToken)
                ?? throw new YandexDiskException(HttpStatusCode.Unauthorized, AppResources.CloudSignedOut);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ApiTimeout);

            using var request = new HttpRequestMessage(method, YandexConfig.DiskApiUrl + relativeUrl);
            request.Headers.Authorization = new("OAuth", token);

            var response = await _http.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
                return response;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();

            // The user revoked access in their Yandex ID settings.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                _auth.SignOut();

            throw YandexDiskException.FromStatus(response.StatusCode, body);
        }
    }

    public class YandexDiskException : Exception
    {
        public YandexDiskException(HttpStatusCode statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }

        public static YandexDiskException FromStatus(HttpStatusCode statusCode, string body)
        {
            var message = statusCode switch
            {
                HttpStatusCode.RequestEntityTooLarge => AppResources.CloudFileTooLarge,
                HttpStatusCode.InsufficientStorage => AppResources.CloudNoSpace,
                HttpStatusCode.Unauthorized => AppResources.CloudSignedOut,
                _ => string.Format(AppResources.CloudDiskError, ReadDescription(body) ?? ((int)statusCode).ToString())
            };
            return new YandexDiskException(statusCode, message);
        }

        private static string? ReadDescription(string body)
        {
            try
            {
                using var json = JsonDocument.Parse(body);
                return json.RootElement.TryGetProperty("description", out var description) ? description.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
