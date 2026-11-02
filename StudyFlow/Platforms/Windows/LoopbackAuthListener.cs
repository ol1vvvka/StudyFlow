using System.Net;
using System.Net.Sockets;
using System.Text;
using StudyFlow.Resources.Strings;

namespace StudyFlow.WinUI
{
    /// <summary>
    /// OAuth sign-in on Windows, where WebAuthenticator is not available: opens the sign-in page in the
    /// default browser and waits for it to redirect to http://127.0.0.1:port/ with the result.
    /// </summary>
    public static class LoopbackAuthListener
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

        /// <returns>The query parameters of the redirect, or null if cancelled or timed out.</returns>
        public static async Task<IDictionary<string, string>?> WaitForCallbackAsync(Uri authorizeUrl, int port, CancellationToken cancellationToken)
        {
            // A plain socket rather than HttpListener: http.sys may need an admin-only URL reservation.
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            try
            {
                await Launcher.Default.OpenAsync(authorizeUrl);

                while (true)
                {
                    using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                    var stream = client.GetStream();

                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
                    var requestLine = await reader.ReadLineAsync(timeout.Token) ?? string.Empty;

                    // "GET /?code=...&state=... HTTP/1.1"; browsers also ask for /favicon.ico, which has no query.
                    var target = requestLine.Split(' ') is [_, var path, ..] ? path : "/";
                    var queryStart = target.IndexOf('?');
                    if (queryStart < 0)
                    {
                        await RespondAsync(stream, "404 Not Found", string.Empty, timeout.Token);
                        continue;
                    }

                    var page = $"<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\"><title>StudyFlow</title></head>" +
                               $"<body style=\"font-family:sans-serif;padding:48px\"><h2>{WebUtility.HtmlEncode(AppResources.YandexBrowserDone)}</h2></body></html>";
                    await RespondAsync(stream, "200 OK", page, timeout.Token);
                    return ParseQuery(target[(queryStart + 1)..]);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task RespondAsync(Stream stream, string status, string html, CancellationToken cancellationToken)
        {
            var body = Encoding.UTF8.GetBytes(html);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                var key = separator < 0 ? pair : pair[..separator];
                var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
                result[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            return result;
        }
    }
}
