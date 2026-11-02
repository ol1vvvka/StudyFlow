using System.Text.Json.Serialization;

namespace StudyFlow.Models
{
    public class Subject
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = SubjectPalette.DefaultColor;

        public List<StudyTask> Tasks { get; set; } = [];

        public List<Tag> Tags { get; set; } = [];

        /// <summary>
        /// Filled only from seed data. Load a subject's notes with <see cref="Data.NoteRepository"/>.
        /// </summary>
        public List<Note> Notes { get; set; } = [];

        [JsonIgnore]
        public int MaterialCount { get; set; }

        [JsonIgnore]
        public int NoteCount { get; set; }

        [JsonIgnore]
        public Color DisplayColor => Microsoft.Maui.Graphics.Color.FromArgb(Color);

        [JsonIgnore]
        public bool HasTeacher => !string.IsNullOrWhiteSpace(Teacher);

        [JsonIgnore]
        public string Stats => string.Format(AppResources.SubjectStats, MaterialCount, NoteCount, Tasks.Count);

        [JsonIgnore]
        public string AccessibilityDescription => $"{Name}. {Teacher}. {Description}";

        public override string ToString() => $"{Name}";
    }

    public class SubjectsJson
    {
        public List<Subject> Subjects { get; set; } = [];
    }
}
