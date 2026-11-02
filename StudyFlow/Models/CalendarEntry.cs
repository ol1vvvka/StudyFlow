namespace StudyFlow.Models;

public enum CalendarEntryKind { Event, Activity, Task, Meeting }
public enum CalendarViewMode { Year, Month, Week, Day }

public sealed class CalendarEntry
{
    public int ID { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public int? SubjectID { get; set; }
    public CalendarEntryKind Kind { get; set; }
    // Calendar times are local wall-clock times; an all-day end is exclusive.
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsCompleted { get; set; }

    public CalendarEntry Copy() => (CalendarEntry)MemberwiseClone();
}
