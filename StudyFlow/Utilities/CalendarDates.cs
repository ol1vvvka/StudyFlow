using StudyFlow.Models;

namespace StudyFlow.Utilities;

public static class CalendarDates
{
    public static DateTime Monday(DateTime date) => date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    public static (DateTime Start, DateTime End) Range(DateTime date, CalendarViewMode mode)
    {
        var start = mode switch
        {
            CalendarViewMode.Year => new DateTime(date.Year, 1, 1),
            CalendarViewMode.Month => new DateTime(date.Year, date.Month, 1),
            CalendarViewMode.Week => Monday(date),
            _ => date.Date
        };
        return (start, mode switch
        {
            CalendarViewMode.Year => start.AddYears(1),
            CalendarViewMode.Month => start.AddMonths(1),
            CalendarViewMode.Week => start.AddDays(7),
            _ => start.AddDays(1)
        });
    }

    public static bool Overlaps(CalendarEntry entry, DateTime start, DateTime end) =>
        entry.Start < end && entry.End > start;

    public static void Validate(CalendarEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Title))
            throw new ArgumentException(CalendarText.Get("TitleRequired"));
        if (entry.End <= entry.Start)
            throw new ArgumentException(CalendarText.Get("EndAfterStart"));
        if (entry.Start.Year < 1900 || entry.End.Year > 2100)
            throw new ArgumentException(CalendarText.Get("DateRange"));
        if (!Enum.IsDefined(entry.Kind))
            throw new ArgumentException(CalendarText.Get("InvalidKind"));
        if (entry.IsAllDay && (entry.Start.TimeOfDay != TimeSpan.Zero || entry.End.TimeOfDay != TimeSpan.Zero))
            throw new ArgumentException(CalendarText.Get("InvalidAllDay"));
    }
}
