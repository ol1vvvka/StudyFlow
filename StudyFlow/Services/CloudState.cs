namespace StudyFlow.Services
{
    /// <summary>
    /// Whether the user is signed in to Yandex, so lists know if sync icons make sense.
    /// Kept up to date by <see cref="Yandex.YandexAuthService"/>.
    /// </summary>
    public static class CloudState
    {
        public static bool IsConnected { get; set; }
    }
}
