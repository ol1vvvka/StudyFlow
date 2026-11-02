using System.Security.Cryptography;
using System.Text;

namespace StudyFlow.Services.Yandex
{
    /// <summary>
    /// Random values for OAuth 2.0 PKCE (RFC 7636) and the anti-forgery state parameter.
    /// </summary>
    public static class Pkce
    {
        // 32 random bytes give a 43-character verifier, the minimum length RFC 7636 allows.
        public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

        public static string CreateChallenge(string verifier) =>
            Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(16));

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
