using System.Text.Json.Serialization;

namespace StudyFlow.Models
{
    public class StudyTask
    {
        public int ID { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }

        [JsonIgnore]
        public int SubjectID { get; set; }
    }
}
