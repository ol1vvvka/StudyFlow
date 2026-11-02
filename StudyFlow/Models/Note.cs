using System.Text.Json.Serialization;

namespace StudyFlow.Models
{
    /// <summary>
    /// A text note. <see cref="SubjectID"/> is 0 for a note that does not belong to any subject.
    /// </summary>
    public class Note
    {
        private const int PreviewLength = 200;

        public int ID { get; set; }

        [JsonIgnore]
        public int SubjectID { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// When the note was created, in UTC.
        /// </summary>
        [JsonIgnore]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the note was last changed, in UTC.
        /// </summary>
        [JsonIgnore]
        public DateTime UpdatedAt { get; set; }

        [JsonIgnore]
        public string SubjectName { get; set; } = string.Empty;

        [JsonIgnore]
        public string SubjectColor { get; set; } = SubjectPalette.DefaultColor;

        /// <summary>
        /// Where the note was last uploaded on Yandex Disk ("app:/....md"); empty if never.
        /// </summary>
        [JsonIgnore]
        public string RemotePath { get; set; } = string.Empty;

        [JsonIgnore]
        public SyncStatus SyncStatus { get; set; }

        [JsonIgnore]
        public string SyncError { get; set; } = string.Empty;

        /// <summary>
        /// Grows on every local change, so an upload that finishes after a newer change does not mark it as synced.
        /// </summary>
        [JsonIgnore]
        public int SyncVersion { get; set; }

        [JsonIgnore]
        public bool ShowSync => CloudState.IsConnected;

        [JsonIgnore]
        public string SyncGlyph => SyncStatus switch
        {
            SyncStatus.Synced => FluentUI.cloud_checkmark_16_regular,
            SyncStatus.Error => FluentUI.cloud_dismiss_16_regular,
            _ => FluentUI.cloud_arrow_up_16_regular
        };

        [JsonIgnore]
        public Color SyncColor => SyncStatus == SyncStatus.Error ? Colors.IndianRed : Colors.Gray;

        [JsonIgnore]
        public bool HasSubject => SubjectID > 0 && SubjectName.Length > 0;

        [JsonIgnore]
        public Color SubjectDisplayColor => Microsoft.Maui.Graphics.Color.FromArgb(SubjectColor);

        [JsonIgnore]
        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? AppResources.NoteUntitled : Title;

        /// <summary>
        /// The start of the content on one line, for lists.
        /// </summary>
        [JsonIgnore]
        public string Preview
        {
            get
            {
                var text = string.Join(' ', Content.Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                return text.Length <= PreviewLength ? text : text[..PreviewLength] + "…";
            }
        }

        [JsonIgnore]
        public bool HasPreview => !string.IsNullOrWhiteSpace(Content);

        [JsonIgnore]
        public string Details => $"{UpdatedAt.ToLocalTime():g}";

        [JsonIgnore]
        public string AccessibilityDescription => $"{DisplayTitle}. {Preview}";

        public override string ToString() => DisplayTitle;
    }
}
