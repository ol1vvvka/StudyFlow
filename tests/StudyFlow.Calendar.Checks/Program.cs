using Microsoft.Data.Sqlite;
using StudyFlow.Data;
using StudyFlow.Models;

SQLitePCL.Batteries_V2.Init();
int assertions = 0;
void Check(bool value, string description)
{
    if (!value) throw new Exception(description);
    assertions++;
}
void Invalid(CalendarEntry entry, string description)
{
    try { CalendarDates.Validate(entry); }
    catch (ArgumentException) { assertions++; return; }
    throw new Exception(description);
}
var day = new DateTime(2026, 11, 2);
CalendarEntry Entry(DateTime start, DateTime end) => new() { Title = "Запись", Start = start, End = end };
Check(CalendarDates.Monday(day.AddDays(6)) == day, "Sunday belongs to the preceding Monday");
Check(CalendarDates.Range(new DateTime(2024, 2, 29), CalendarViewMode.Month) == (new DateTime(2024, 2, 1), new DateTime(2024, 3, 1)), "Leap-month boundary");
Check(CalendarDates.Range(day, CalendarViewMode.Year) == (new DateTime(2026, 1, 1), new DateTime(2027, 1, 1)), "Year boundary");
Invalid(Entry(day, day), "Zero duration must be rejected");
Invalid(Entry(day, day.AddMinutes(-1)), "Negative duration must be rejected");
var blank = Entry(day, day.AddHours(1)); blank.Title = "  ";
Invalid(blank, "Blank title must be rejected");
var allDay = Entry(day, day.AddDays(2)); allDay.IsAllDay = true;
CalendarDates.Validate(allDay);
Check(CalendarDates.Overlaps(allDay, day.AddDays(1), day.AddDays(2)), "All-day includes its second day");
Check(!CalendarDates.Overlaps(allDay, day.AddDays(2), day.AddDays(3)), "All-day end is exclusive");
var invalidAllDay = allDay.Copy(); invalidAllDay.End = invalidAllDay.End.AddHours(1);
Invalid(invalidAllDay, "All-day must have midnight boundaries");
var a = Entry(day.AddHours(9), day.AddHours(11));
var b = Entry(day.AddHours(10), day.AddHours(12));
var c = Entry(day.AddHours(12), day.AddHours(13));
var placements = CalendarLayout.Arrange([a, b, c], day);
Check(placements[0].Column != placements[1].Column && placements[0].Columns == 2, "Overlapping events get separate lanes");
Check(placements[2].Column == 0 && placements[2].Columns == 1, "Adjacent events reuse full width");
var overnight = Entry(day.AddHours(23), day.AddDays(1).AddHours(2));
Check(CalendarLayout.Arrange([overnight], day)[0].EndMinute == 1440, "Overnight is clipped at midnight");
Check(CalendarLayout.Arrange([overnight], day.AddDays(1))[0].StartMinute == 0, "Overnight resumes at midnight");
Check(CalendarLayout.Arrange([allDay], day).Count == 0, "All-day entries stay outside the timed grid");
var path = Path.Combine(Path.GetTempPath(), $"studyflow-calendar-check-{Guid.NewGuid():N}.db");
try
{
    var repository = new CalendarRepository(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
    Check((await repository.ListAsync(day, day.AddDays(1))).Count == 0, "A new database initializes without changing existing tables");
    overnight.Title = "Встреча 'SQL'; — тест";
    overnight.Kind = CalendarEntryKind.Meeting;
    overnight.SubjectID = 42;
    await repository.SaveAsync(overnight);
    await repository.SaveAsync(allDay);
    Check(overnight.ID > 0, "Insert returns its generated ID");
    var reopened = new CalendarRepository(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
    var nextDay = await reopened.ListAsync(day.AddDays(1), day.AddDays(2));
    var loaded = nextDay.Single(e => e.ID == overnight.ID);
    Check(loaded.Title == overnight.Title && loaded.SubjectID == 42 && loaded.End == overnight.End, "Data persists, including quotes, subject and next-day end");
    Check(nextDay.Count == 2, "Both multi-day entries appear on the next day");
    Check((await reopened.ListAsync(day.AddDays(2), day.AddDays(3))).Count == 0, "No entry leaks past its end");
    loaded.Title = "Обновлено"; loaded.Kind = CalendarEntryKind.Task; loaded.IsCompleted = true;
    await reopened.SaveAsync(loaded);
    var updated = await repository.ListAsync(day, day.AddDays(1));
    Check(updated.Count == 2 && updated.Single(e => e.ID == loaded.ID).IsCompleted, "Update preserves ID and saves task completion");
    await repository.DeleteAsync(loaded.ID);
    Check((await reopened.ListAsync(day, day.AddDays(1))).Count == 1, "Delete affects only the selected entry");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (File.Exists(path)) File.Delete(path);
}
Console.WriteLine($"Calendar checks passed: {assertions}");
