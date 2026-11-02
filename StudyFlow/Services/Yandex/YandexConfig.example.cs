namespace StudyFlow.Services.Yandex
{
    /// <summary>
    /// Settings of the StudyFlow app registered at oauth.yandex.ru. How to register it: docs/YANDEX.md.
    /// </summary>
    public static class YandexConfig
    {
        // ClientID is public by design. The app has no client secret: it would be easy to pull out of the APK,
        // so sign-in uses PKCE instead (Yandex accepts code_verifier in place of the secret).
        public const string ClientId = "";

        public const string AuthorizeUrl = "https://oauth.yandex.ru/authorize";
        public const string TokenUrl = "https://oauth.yandex.ru/token";
        public const string UserInfoUrl = "https://login.yandex.ru/info?format=json";
        public const string DiskApiUrl = "https://cloud-api.yandex.net/v1/disk/";

        public const string CallbackScheme = "ru.studyflow.app";

        // WebAuthenticator does not work on Windows, so there the browser redirects to a local listener on this port.
        public const int LoopbackPort = 47563;

        public static string RedirectUri =>
            OperatingSystem.IsWindows() ? $"http://127.0.0.1:{LoopbackPort}/" : $"{CallbackScheme}://auth";

        public static bool IsConfigured => ClientId.Length > 0;
    }
}
