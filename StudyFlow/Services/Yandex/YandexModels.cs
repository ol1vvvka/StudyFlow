using System.Text.Json.Serialization;

namespace StudyFlow.Services.Yandex
{
    /// <summary>
    /// The signed-in Yandex user, as returned by login.yandex.ru/info and kept in SecureStorage.
    /// </summary>
    public class YandexUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("default_email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("default_avatar_id")]
        public string AvatarId { get; set; } = string.Empty;

        [JsonPropertyName("is_avatar_empty")]
        public bool IsAvatarEmpty { get; set; }

        [JsonIgnore]
        public string Name => string.IsNullOrWhiteSpace(DisplayName) ? Login : DisplayName;

        [JsonIgnore]
        public string? AvatarUrl => IsAvatarEmpty || AvatarId.Length == 0
            ? null
            : $"https://avatars.yandex.net/get-yapic/{AvatarId}/islands-retina-middle";
    }

    public class YandexTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }
    }

    public class YandexDiskInfo
    {
        [JsonPropertyName("total_space")]
        public long TotalSpace { get; set; }

        [JsonPropertyName("used_space")]
        public long UsedSpace { get; set; }
    }

    public class YandexAuthException : Exception
    {
        public YandexAuthException(string message) : base(message)
        {
        }
    }
}
