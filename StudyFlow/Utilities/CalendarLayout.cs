using StudyFlow.Models;

namespace StudyFlow.Utilities;

public sealed record CalendarPlacement(CalendarEntry Entry, double StartMinute, double EndMinute, int Column, int Columns);

public static class CalendarLayout
{
    public static List<CalendarPlacement> Arrange(IEnumerable<CalendarEntry> entries, DateTime day)
    {
        var segments = entries.Where(e => !e.IsAllDay && CalendarDates.Overlaps(e, day.Date, day.Date.AddDays(1)))
            .Select(e => (Entry: e, Start: Math.Max(0, (e.Start - day.Date).TotalMinutes), End: Math.Min(1440, (e.End - day.Date).TotalMinutes)))
            .OrderBy(e => e.Start).ThenBy(e => e.End).ToList();
        var result = new List<CalendarPlacement>();
        var group = new List<(CalendarEntry Entry, double Start, double End, int Column)>();
        var ends = new List<double>();
        void Flush()
        {
            result.AddRange(group.Select(e => new CalendarPlacement(e.Entry, e.Start, e.End, e.Column, ends.Count)));
            group.Clear(); ends.Clear();
        }
        foreach (var segment in segments)
        {
            if (group.Count > 0 && ends.All(end => end <= segment.Start)) Flush();
            int column = ends.FindIndex(end => end <= segment.Start);
            if (column < 0) { column = ends.Count; ends.Add(segment.End); }
            else ends[column] = segment.End;
            group.Add((segment.Entry, segment.Start, segment.End, column));
        }
        Flush();
        return result;
    }
}
