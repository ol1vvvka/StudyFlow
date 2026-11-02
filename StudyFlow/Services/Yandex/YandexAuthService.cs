using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace StudyFlow.Services.Yandex
{
    /// <summary>
    /// Signs the user in with Yandex ID (OAuth 2.0 authorization code + PKCE) and keeps the tokens in SecureStorage.
    /// Signing in is optional: without it the app works only with data on this device.
    /// </summary>
    public class YandexAuthService
    {
        private const string AccessTokenKey = "yandex_access_token";
        private const string RefreshTokenKey = "yandex_refresh_token";
        private const string ExpiresAtKey = "yandex_expires_at";
        private const string UserKey = "yandex_user";

        // Refresh a little early so a request never starts with a token that expires on the way.
        private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(5);

        // The shared HttpClient has no timeout of its own (uploads can be long).
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private readonly HttpClient _http;
        private readonly ILogger<YandexAuthService> _logger;
        private bool _initialized;

        public YandexAuthService(HttpClient http, ILogger<YandexAuthService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public YandexUser? CurrentUser { get; private set; }

        public bool IsSignedIn => CurrentUser is not null;

        /// <summary>
        /// Restores the signed-in user saved by a previous session.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            try
            {
                var json = await SecureStorage.Default.GetAsync(UserKey);
                if (json is not null)
                    CurrentUser = JsonSerializer.Deserialize(json, JsonContext.Default.YandexUser);
                CloudState.IsConnected = CurrentUser is not null;
            }
            catch (Exception e)
            {
                // SecureStorage can fail after an app reinstall on Android; the user simply signs in again.
                _logger.LogWarning(e, "Could not restore the Yandex session");
                SignOut();
            }

            _initialized = true;
        }

        /// <summary>
        /// Opens the Yandex ID sign-in page and waits for the user to come back.
        /// </summary>
        /// <returns>The signed-in user, or null if the user cancelled.</returns>
        public async Task<YandexUser?> SignInAsync(CancellationToken cancellationToken = default)
        {
            if (!YandexConfig.IsConfigured)
                throw new YandexAuthException(AppResources.YandexNotConfigured);

            var verifier = Pkce.CreateVerifier();
            var state = Pkce.CreateState();
            var authorizeUrl = new Uri(
                $"{YandexConfig.AuthorizeUrl}?response_type=code" +
                $"&client_id={Uri.EscapeDataString(YandexConfig.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(YandexConfig.RedirectUri)}" +
                $"&code_challenge={Pkce.CreateChallenge(verifier)}&code_challenge_method=S256" +
                $"&state={state}" +
                $"&device_name={Uri.EscapeDataString(DeviceInfo.Current.Name)}");

            var callback = await GetCallbackAsync(authorizeUrl, cancellationToken);
            if (callback is null)
                return null;

            if (callback.TryGetValue("error", out var error))
            {
                // The user pressed "Cancel" on the Yandex consent page.
                if (error == "access_denied")
                    return null;

                var description = callback.TryGetValue("error_description", out var text) ? text : error;
                throw new YandexAuthException(string.Format(AppResources.YandexAuthFailed, description));
            }

            if (!callback.TryGetValue("state", out var returnedState) || returnedState != state)
                throw new YandexAuthException(AppResources.YandexStateMismatch);

            if (!callback.TryGetValue("code", out var code))
                throw new YandexAuthException(string.Format(AppResources.YandexAuthFailed, "no code"));

            var token = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = YandexConfig.ClientId,
                ["code_verifier"] = verifier
            }, cancellationToken);
            await SaveTokenAsync(token);

            var user = await LoadUserAsync(token.AccessToken, cancellationToken);
            await SecureStorage.Default.SetAsync(UserKey, JsonSerializer.Serialize(user, JsonContext.Default.YandexUser));
            CurrentUser = user;
            CloudState.IsConnected = true;
            return user;
        }

        /// <summary>
        /// Forgets the tokens on this device. Data on Yandex Disk and on the device stays.
        /// </summary>
        public void SignOut()
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
            SecureStorage.Default.Remove(ExpiresAtKey);
            SecureStorage.Default.Remove(UserKey);
            CurrentUser = null;
            CloudState.IsConnected = false;
        }

        /// <summary>
        /// Returns a valid access token, refreshing it when it is about to expire.
        /// </summary>
        /// <returns>The token, or null if the user has to sign in again.</returns>
        public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
            if (accessToken is null)
                return null;

            var expiresAtText = await SecureStorage.Default.GetAsync(ExpiresAtKey);
            if (!DateTime.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
                || expiresAt - ExpiryMargin > DateTime.UtcNow)
            {
                return accessToken;
            }

            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (refreshToken is null)
            {
                SignOut();
                return null;
            }

            try
            {
                var token = await RequestTokenAsync(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = YandexConfig.ClientId
                }, cancellationToken);
                await SaveTokenAsync(token);
                return token.AccessToken;
            }
            catch (YandexAuthException e)
            {
                _logger.LogWarning(e, "Could not refresh the Yandex token");
                SignOut();
                return null;
            }
        }

        private static async Task<IDictionary<string, string>?> GetCallbackAsync(Uri authorizeUrl, CancellationToken cancellationToken)
        {
#if WINDOWS
            return await StudyFlow.WinUI.LoopbackAuthListener.WaitForCallbackAsync(authorizeUrl, YandexConfig.LoopbackPort, cancellationToken);
#else
            try
            {
                var result = await WebAuthenticator.Default.AuthenticateAsync(authorizeUrl, new Uri(YandexConfig.RedirectUri));
                return result.Properties;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
#endif
        }

        private async Task<YandexTokenResponse> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);

            using var response = await _http.PostAsync(YandexConfig.TokenUrl, new FormUrlEncodedContent(form), timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
                throw new YandexAuthException(string.Format(AppResources.YandexAuthFailed, ReadError(body)));

            var token = JsonSerializer.Deserialize(body, JsonContext.Default.YandexTokenResponse);
            if (token is null || token.AccessToken.Length == 0)
                throw new YandexAuthException(string.Format(AppResources.YandexAuthFailed, "empty token"));

            return token;
        }

        private async Task SaveTokenAsync(YandexTokenResponse token)
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, token.AccessToken);

            // Yandex may return the same token without a new refresh token; keep the old one then.
            if (!string.IsNullOrEmpty(token.RefreshToken))
                await SecureStorage.Default.SetAsync(RefreshTokenKey, token.RefreshToken);

            var expiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            await SecureStorage.Default.SetAsync(ExpiresAtKey, expiresAt.ToString("o", CultureInfo.InvariantCulture));
        }

        private async Task<YandexUser> LoadUserAsync(string accessToken, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, YandexConfig.UserInfoUrl);
            request.Headers.Authorization = new("OAuth", accessToken);

            using var response = await _http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return JsonSerializer.Deserialize(body, JsonContext.Default.YandexUser)
                ?? throw new YandexAuthException(string.Format(AppResources.YandexAuthFailed, "empty profile"));
        }

        private static string ReadError(string body)
        {
            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("error_description", out var description))
                    return description.GetString() ?? body;
                if (json.RootElement.TryGetProperty("error", out var error))
                    return error.GetString() ?? body;
            }
            catch (JsonException)
            {
            }

            return body;
        }
    }
}
