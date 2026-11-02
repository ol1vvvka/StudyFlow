using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StudyFlow.Models;
using System.Globalization;

namespace StudyFlow.Data
{
    /// <summary>
    /// Repository class for managing notes in the database.
    /// </summary>
    public class NoteRepository
    {
        private const string SelectSql = @"
            SELECT n.ID, n.SubjectID, n.Title, n.Content, n.CreatedAt, n.UpdatedAt, s.Name, s.Color,
                   n.RemotePath, n.SyncStatus, n.SyncError, n.SyncVersion
            FROM Note n
            LEFT JOIN Subject s ON s.ID = n.SubjectID";

        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoteRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public NoteRepository(ILogger<NoteRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database connection and creates the Note table if it does not exist.
        /// </summary>
        private async Task Init()
        {
            if (_hasBeenInitialized)
                return;

            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            try
            {
                // Subject is created here as well because the list queries join it;
                // RemoteDeletion because deleting an uploaded note writes to it.
                var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = SubjectRepository.CreateTableSql + RemoteDeletionRepository.CreateTableSql + @"
            CREATE TABLE IF NOT EXISTS Note (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                SubjectID INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                RemotePath TEXT NOT NULL DEFAULT '',
                SyncStatus INTEGER NOT NULL DEFAULT 0,
                SyncError TEXT NOT NULL DEFAULT '',
                SyncVersion INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_Note_SubjectID ON Note (SubjectID);";
                await createTableCmd.ExecuteNonQueryAsync();
                await SchemaMigrations.AddSyncColumnsAsync(connection, "Note");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating Note table");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves all notes, most recently changed first.
        /// </summary>
        public Task<List<Note>> ListAsync() =>
            QueryAsync($"{SelectSql} ORDER BY n.UpdatedAt DESC");

        /// <summary>
        /// Retrieves the notes of a specific subject, most recently changed first.
        /// </summary>
        /// <param name="subjectId">The ID of the subject.</param>
        public Task<List<Note>> ListAsync(int subjectId) =>
            QueryAsync($"{SelectSql} WHERE n.SubjectID = @subjectId ORDER BY n.UpdatedAt DESC",
                cmd => cmd.Parameters.AddWithValue("@subjectId", subjectId));

        /// <summary>
        /// Retrieves a specific note by its ID.
        /// </summary>
        /// <param name="id">The ID of the note.</param>
        /// <returns>A <see cref="Note"/> object if found; otherwise, null.</returns>
        public async Task<Note?> GetAsync(int id)
        {
            var notes = await QueryAsync($"{SelectSql} WHERE n.ID = @id",
                cmd => cmd.Parameters.AddWithValue("@id", id));

            return notes.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the notes that still have to be sent to Yandex Disk.
        /// </summary>
        /// <param name="includeErrors">Also retry notes whose last upload failed.</param>
        public Task<List<Note>> ListPendingAsync(bool includeErrors) =>
            QueryAsync($"{SelectSql} WHERE n.SyncStatus = @pending OR (@includeErrors AND n.SyncStatus = @error) ORDER BY n.UpdatedAt",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@pending", (int)SyncStatus.Pending);
                    cmd.Parameters.AddWithValue("@error", (int)SyncStatus.Error);
                    cmd.Parameters.AddWithValue("@includeErrors", includeErrors);
                });

        /// <summary>
        /// Records the result of sending a note to Disk.
        /// The status is kept as pending if the note changed while it was being sent.
        /// </summary>
        public async Task UpdateSyncStateAsync(Note item, string remotePath, SyncStatus status, string error)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Note SET RemotePath = @RemotePath WHERE ID = @ID;
                UPDATE Note SET SyncStatus = @SyncStatus, SyncError = @SyncError
                WHERE ID = @ID AND SyncVersion = @SyncVersion;";
            updateCmd.Parameters.AddWithValue("@ID", item.ID);
            updateCmd.Parameters.AddWithValue("@RemotePath", remotePath);
            updateCmd.Parameters.AddWithValue("@SyncStatus", (int)status);
            updateCmd.Parameters.AddWithValue("@SyncError", error);
            updateCmd.Parameters.AddWithValue("@SyncVersion", item.SyncVersion);

            await updateCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Counts notes whose last upload to Disk failed.
        /// </summary>
        public async Task<int> CountSyncErrorsAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Note WHERE SyncStatus = @error";
            countCmd.Parameters.AddWithValue("@error", (int)SyncStatus.Error);

            return Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// Marks all notes of a subject for sending again, e.g. after the subject was renamed.
        /// </summary>
        public async Task MarkSubjectPendingAsync(int subjectId)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE Note SET SyncStatus = 0, SyncVersion = SyncVersion + 1 WHERE SubjectID = @subjectId";
            updateCmd.Parameters.AddWithValue("@subjectId", subjectId);

            await updateCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Counts notes per subject.
        /// </summary>
        /// <returns>Note count by subject ID.</returns>
        public async Task<Dictionary<int, int>> CountBySubjectAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT SubjectID, COUNT(*) FROM Note GROUP BY SubjectID";
            var counts = new Dictionary<int, int>();

            await using var reader = await countCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                counts[reader.GetInt32(0)] = reader.GetInt32(1);
            }

            return counts;
        }

        /// <summary>
        /// Saves a note to the database. If the note ID is 0, a new note is created; otherwise, the existing note is updated.
        /// </summary>
        /// <param name="item">The note to save.</param>
        /// <returns>The ID of the saved note.</returns>
        public async Task<int> SaveItemAsync(Note item)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var saveCmd = connection.CreateCommand();
            if (item.ID == 0)
            {
                saveCmd.CommandText = @"
                INSERT INTO Note (SubjectID, Title, Content, CreatedAt, UpdatedAt)
                VALUES (@SubjectID, @Title, @Content, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();";
            }
            else
            {
                // Any change has to reach Disk too, so the note goes back to pending.
                saveCmd.CommandText = @"
                UPDATE Note
                SET SubjectID = @SubjectID, Title = @Title, Content = @Content, CreatedAt = @CreatedAt, UpdatedAt = @UpdatedAt,
                    SyncStatus = 0, SyncVersion = SyncVersion + 1
                WHERE ID = @ID";
                saveCmd.Parameters.AddWithValue("@ID", item.ID);
            }

            saveCmd.Parameters.AddWithValue("@SubjectID", item.SubjectID);
            saveCmd.Parameters.AddWithValue("@Title", item.Title);
            saveCmd.Parameters.AddWithValue("@Content", item.Content);
            // ISO 8601 in UTC sorts correctly as text, so ORDER BY UpdatedAt works.
            saveCmd.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            saveCmd.Parameters.AddWithValue("@UpdatedAt", item.UpdatedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));

            var result = await saveCmd.ExecuteScalarAsync();
            if (item.ID == 0)
            {
                item.ID = Convert.ToInt32(result);
            }

            return item.ID;
        }

        /// <summary>
        /// Deletes a note from the database and queues its copy on Disk for deletion.
        /// </summary>
        /// <param name="item">The note to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(Note item)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = @"
            INSERT INTO RemoteDeletion (Path) SELECT RemotePath FROM Note WHERE ID = @id AND RemotePath <> '';
            DELETE FROM Note WHERE ID = @id;";
            deleteCmd.Parameters.AddWithValue("@id", item.ID);

            return await deleteCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Deletes all notes of a specific subject and queues their copies on Disk for deletion.
        /// </summary>
        /// <param name="subjectId">The ID of the subject.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteBySubjectAsync(int subjectId)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = @"
            INSERT INTO RemoteDeletion (Path) SELECT RemotePath FROM Note WHERE SubjectID = @subjectId AND RemotePath <> '';
            DELETE FROM Note WHERE SubjectID = @subjectId;";
            deleteCmd.Parameters.AddWithValue("@subjectId", subjectId);

            return await deleteCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Drops the Note table from the database.
        /// </summary>
        public async Task DropTableAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var dropTableCmd = connection.CreateCommand();
            dropTableCmd.CommandText = "DROP TABLE IF EXISTS Note";
            await dropTableCmd.ExecuteNonQueryAsync();
            _hasBeenInitialized = false;
        }

        private async Task<List<Note>> QueryAsync(string sql, Action<SqliteCommand>? addParameters = null)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = sql;
            addParameters?.Invoke(selectCmd);
            var notes = new List<Note>();

            await using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                notes.Add(new Note
                {
                    ID = reader.GetInt32(0),
                    SubjectID = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Content = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    UpdatedAt = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    SubjectName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    SubjectColor = reader.IsDBNull(7) ? SubjectPalette.DefaultColor : reader.GetString(7),
                    RemotePath = reader.GetString(8),
                    SyncStatus = (SyncStatus)reader.GetInt32(9),
                    SyncError = reader.GetString(10),
                    SyncVersion = reader.GetInt32(11)
                });
            }

            return notes;
        }
    }
}
