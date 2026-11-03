using Microsoft.Data.Sqlite;
using StudyFlow.Models;
using System.Globalization;

namespace StudyFlow.Data;

public sealed class CalendarRepository
{
    private readonly string _connectionString;
    public CalendarRepository() : this(Constants.DatabasePath) { }
    public CalendarRepository(string connectionString) => _connectionString = connectionString;

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS CalendarEntry (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL, Description TEXT NOT NULL, Location TEXT NOT NULL,
                    SubjectID INTEGER NULL, Kind INTEGER NOT NULL,
                    StartsAt TEXT NOT NULL, EndsAt TEXT NOT NULL,
                    IsAllDay INTEGER NOT NULL, IsCompleted INTEGER NOT NULL,
                    CHECK (EndsAt > StartsAt)
                );
                CREATE INDEX IF NOT EXISTS IX_CalendarEntry_Start ON CalendarEntry (StartsAt);
                """;
            await command.ExecuteNonQueryAsync();
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<List<CalendarEntry>> ListAsync(DateTime start, DateTime end)
    {
        await using var connection = await OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ID, Title, Description, Location, SubjectID, Kind, StartsAt, EndsAt, IsAllDay, IsCompleted FROM CalendarEntry WHERE StartsAt < @end AND EndsAt > @start ORDER BY IsAllDay DESC, StartsAt, ID";
        command.Parameters.AddWithValue("@start", Format(start));
        command.Parameters.AddWithValue("@end", Format(end));
        var entries = new List<CalendarEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            entries.Add(new CalendarEntry
            {
                ID = reader.GetInt32(0), Title = reader.GetString(1), Description = reader.GetString(2),
                Location = reader.GetString(3), SubjectID = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Kind = (CalendarEntryKind)reader.GetInt32(5), Start = Parse(reader.GetString(6)), End = Parse(reader.GetString(7)),
                IsAllDay = reader.GetBoolean(8), IsCompleted = reader.GetBoolean(9)
            });
        return entries;
    }

    public async Task SaveAsync(CalendarEntry entry)
    {
        CalendarDates.Validate(entry);
        await using var connection = await OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = entry.ID == 0
            ? "INSERT INTO CalendarEntry (Title, Description, Location, SubjectID, Kind, StartsAt, EndsAt, IsAllDay, IsCompleted) VALUES (@title, @description, @location, @subject, @kind, @start, @end, @allDay, @completed); SELECT last_insert_rowid();"
            : "UPDATE CalendarEntry SET Title=@title, Description=@description, Location=@location, SubjectID=@subject, Kind=@kind, StartsAt=@start, EndsAt=@end, IsAllDay=@allDay, IsCompleted=@completed WHERE ID=@id;";
        command.Parameters.AddWithValue("@id", entry.ID);
        command.Parameters.AddWithValue("@title", entry.Title.Trim());
        command.Parameters.AddWithValue("@description", entry.Description);
        command.Parameters.AddWithValue("@location", entry.Location.Trim());
        command.Parameters.AddWithValue("@subject", (object?)entry.SubjectID ?? DBNull.Value);
        command.Parameters.AddWithValue("@kind", (int)entry.Kind);
        command.Parameters.AddWithValue("@start", Format(entry.Start));
        command.Parameters.AddWithValue("@end", Format(entry.End));
        command.Parameters.AddWithValue("@allDay", entry.IsAllDay);
        command.Parameters.AddWithValue("@completed", entry.Kind == CalendarEntryKind.Task && entry.IsCompleted);
        if (entry.ID == 0) entry.ID = Convert.ToInt32(await command.ExecuteScalarAsync());
        else await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var connection = await OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CalendarEntry WHERE ID=@id";
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ClearAllAsync()
    {
        await using var connection = await OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CalendarEntry";
        await command.ExecuteNonQueryAsync();
    }

    private static string Format(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
    private static DateTime Parse(string value) => DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
}
