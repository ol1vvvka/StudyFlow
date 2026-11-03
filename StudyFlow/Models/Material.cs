namespace StudyFlow.Models
{
    public enum MaterialKind
    {
        Other = 0,
        Pdf = 1,
        Document = 2,
        Presentation = 3,
        Spreadsheet = 4,
        Image = 5,
        Video = 6,
        Audio = 7,
        Link = 8
    }

    /// <summary>
    /// A file or a link. <see cref="SubjectID"/> is 0 when the material is not in a space.
    /// </summary>
    public class Material
    {
        public int ID { get; set; }

        /// <summary>
        /// Owning space ID, or 0 for materials added outside any space.
        /// </summary>
        public int SubjectID { get; set; }
        public string Title { get; set; } = string.Empty;
        public MaterialKind Kind { get; set; }

        /// <summary>
        /// Path relative to <see cref="Data.Constants.MaterialsDirectory"/>, with '/' separators. Empty for links.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;
        public long SizeBytes { get; set; }

        /// <summary>
        /// When the material was added, in UTC.
        /// </summary>
        public DateTime AddedAt { get; set; }

        public string SubjectName { get; set; } = string.Empty;
        public string SubjectColor { get; set; } = SubjectPalette.DefaultColor;

        /// <summary>
        /// Where the file was last uploaded on Yandex Disk ("app:/..."); empty if never.
        /// </summary>
        public string RemotePath { get; set; } = string.Empty;

        public SyncStatus SyncStatus { get; set; }
        public string SyncError { get; set; } = string.Empty;

        /// <summary>
        /// Grows on every local change, so an upload that finishes after a newer change does not mark it as synced.
        /// </summary>
        public int SyncVersion { get; set; }

        // Links are not uploaded yet: they will go to Disk with the rest of the app data.
        public bool ShowSync => CloudState.IsConnected && !IsLink;

        public string SyncGlyph => SyncStatus switch
        {
            SyncStatus.Synced => FluentUI.cloud_checkmark_16_regular,
            SyncStatus.Error => FluentUI.cloud_dismiss_16_regular,
            _ => FluentUI.cloud_arrow_up_16_regular
        };

        public Color SyncColor => SyncStatus == SyncStatus.Error ? Colors.IndianRed : Colors.Gray;

        public bool IsLink => Kind == MaterialKind.Link;
        public bool IsImage => Kind == MaterialKind.Image;

        public bool HasSubject => SubjectID > 0 && SubjectName.Length > 0;

        public bool HasLocalFile => !IsLink && FilePath.Length > 0 && File.Exists(FullPath);

        public string FullPath => IsLink || FilePath.Length == 0
            ? string.Empty
            : Path.Combine(Constants.MaterialsDirectory, FilePath);

        public ImageSource? Thumbnail => IsImage && HasLocalFile ? ImageSource.FromFile(FullPath) : null;

        public Color SubjectDisplayColor => Microsoft.Maui.Graphics.Color.FromArgb(
            HasSubject ? SubjectColor : SubjectPalette.DefaultColor);

        public string KindGlyph => Kind switch
        {
            MaterialKind.Pdf => FluentUI.document_pdf_24_regular,
            MaterialKind.Document => FluentUI.document_text_24_regular,
            MaterialKind.Presentation => FluentUI.slide_text_24_regular,
            MaterialKind.Spreadsheet => FluentUI.table_24_regular,
            MaterialKind.Image => FluentUI.image_24_regular,
            MaterialKind.Video => FluentUI.video_clip_24_regular,
            MaterialKind.Audio => FluentUI.headphones_24_regular,
            MaterialKind.Link => FluentUI.link_24_regular,
            _ => FluentUI.document_24_regular
        };

        public string KindName => Kind switch
        {
            MaterialKind.Pdf => AppResources.KindPdf,
            MaterialKind.Document => AppResources.KindDocument,
            MaterialKind.Presentation => AppResources.KindPresentation,
            MaterialKind.Spreadsheet => AppResources.KindSpreadsheet,
            MaterialKind.Image => AppResources.KindImage,
            MaterialKind.Video => AppResources.KindVideo,
            MaterialKind.Audio => AppResources.KindAudio,
            MaterialKind.Link => AppResources.KindLink,
            _ => AppResources.KindOther
        };

        public string Details
        {
            get
            {
                var sizeOrHost = IsLink ? LinkHost : Formatting.FileSize(SizeBytes);
                return $"{KindName} · {sizeOrHost} · {AddedAt.ToLocalTime():d}";
            }
        }

        public string AccessibilityDescription => $"{Title}. {Details}";

        private string LinkHost => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host : Url;

        public override string ToString() => $"{Title}";
    }
}
