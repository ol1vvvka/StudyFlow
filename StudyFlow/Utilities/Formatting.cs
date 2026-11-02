using System.Globalization;

namespace StudyFlow.Utilities
{
    public static class Formatting
    {
        public static string FileSize(long bytes) => bytes switch
        {
            < 1024 => string.Format(AppResources.SizeBytes, bytes),
            < 1024 * 1024 => string.Format(AppResources.SizeKilobytes, bytes / 1024d),
            < 1024L * 1024 * 1024 => string.Format(AppResources.SizeMegabytes, bytes / (1024d * 1024)),
            _ => string.Format(AppResources.SizeGigabytes, bytes / (1024d * 1024 * 1024))
        };

        public static string CapitalizeFirst(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];

        // Characters Windows does not allow in file names; files from Disk may be downloaded there.
        private static readonly char[] InvalidFileNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
        private const int MaxFileNameLength = 100;

        /// <summary>
        /// Turns a title into a file or folder name that is valid on Windows, Android and Yandex Disk.
        /// </summary>
        public static string SafeFileName(string name)
        {
            var safe = new string(name.Select(c => char.IsControl(c) || InvalidFileNameChars.Contains(c) ? '_' : c).ToArray());
            if (safe.Length > MaxFileNameLength)
                safe = safe[..MaxFileNameLength];

            // Windows also rejects names that end with a dot or a space.
            safe = safe.Trim().TrimEnd('.');
            return safe.Length == 0 ? "file" : safe;
        }
    }
}
