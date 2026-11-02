using StudyFlow.Models;

namespace StudyFlow.Utilities
{
    public static class MaterialKinds
    {
        /// <summary>
        /// Guesses the material kind from a file extension such as ".pdf".
        /// </summary>
        public static MaterialKind FromExtension(string extension) => extension.ToLowerInvariant() switch
        {
            ".pdf" => MaterialKind.Pdf,
            ".doc" or ".docx" or ".odt" or ".rtf" or ".txt" or ".md" => MaterialKind.Document,
            ".ppt" or ".pptx" or ".odp" => MaterialKind.Presentation,
            ".xls" or ".xlsx" or ".ods" or ".csv" => MaterialKind.Spreadsheet,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".heic" or ".heif" => MaterialKind.Image,
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" or ".m4v" or ".3gp" => MaterialKind.Video,
            ".mp3" or ".m4a" or ".wav" or ".ogg" or ".aac" or ".flac" or ".opus" => MaterialKind.Audio,
            _ => MaterialKind.Other
        };
    }
}
