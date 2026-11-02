using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StudyFlow.Models;
using System.Globalization;

namespace StudyFlow.Data
{
    /// <summary>
    /// Repository class for managing material records in the database.
    /// The files themselves are handled by <see cref="MaterialService"/>.
    /// </summary>
    public class MaterialRepository
    {
        private const string SelectSql = @"
            SELECT m.ID, m.SubjectID, m.Title, m.Kind, m.FilePath, m.Url, m.SizeBytes, m.AddedAt, s.Name, s.Color,
                   m.RemotePath, m.SyncStatus, m.SyncError, m.SyncVersion
            FROM Material m
            LEFT JOIN Subject s ON s.ID = m.SubjectID";

        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaterialRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public MaterialRepository(ILogger<MaterialRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database connection and creates the Material table if it does not exist.
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
                // RemoteDeletion because deleting an uploaded material writes to it.
                var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = SubjectRepository.CreateTableSql + RemoteDeletionRepository.CreateTableSql + @"
            CREATE TABLE IF NOT EXISTS Material (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                SubjectID INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Kind INTEGER NOT NULL,
                FilePath TEXT NOT NULL,
                Url TEXT NOT NULL,
                SizeBytes INTEGER NOT NULL,
                AddedAt TEXT NOT NULL,
                RemotePath TEXT NOT NULL DEFAULT '',
                SyncStatus INTEGER NOT NULL DEFAULT 0,
                SyncError TEXT NOT NULL DEFAULT '',
                SyncVersion INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_Material_SubjectID ON Material (SubjectID);";
                await createTableCmd.ExecuteNonQueryAsync();
                await SchemaMigrations.AddSyncColumnsAsync(connection, "Material");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating Material table");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves all materials, newest first.
        /// </summary>
        public Task<List<Material>> ListAsync() =>
            QueryAsync($"{SelectSql} ORDER BY m.AddedAt DESC");

        /// <summary>
        /// Retrieves the materials of a specific subject, newest first.
        /// </summary>
        /// <param name="subjectId">The ID of the subject.</param>
        public Task<List<Material>> ListAsync(int subjectId) =>
            QueryAsync($"{SelectSql} WHERE m.SubjectID = @subjectId ORDER BY m.AddedAt DESC",
                cmd => cmd.Parameters.AddWithValue("@subjectId", subjectId));

        /// <summary>
        /// Retrieves the most recently added materials.
        /// </summary>
        /// <param name="count">How many materials to return.</param>
        public Task<List<Material>> ListRecentAsync(int count) =>
            QueryAsync($"{SelectSql} ORDER BY m.AddedAt DESC LIMIT @count",
                cmd => cmd.Parameters.AddWithValue("@count", count));

        /// <summary>
        /// Retrieves a specific material by its ID.
        /// </summary>
        /// <param name="id">The ID of the material.</param>
        /// <returns>A <see cref="Material"/> object if found; otherwise, null.</returns>
        public async Task<Material?> GetAsync(int id)
        {
            var materials = await QueryAsync($"{SelectSql} WHERE m.ID = @id",
                cmd => cmd.Parameters.AddWithValue("@id", id));

            return materials.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the materials that still have to be sent to Yandex Disk.
        /// </summary>
        /// <param name="includeErrors">Also retry materials whose last upload failed.</param>
        public Task<List<Material>> ListPendingAsync(bool includeErrors) =>
            QueryAsync($"{SelectSql} WHERE m.SyncStatus = @pending OR (@includeErrors AND m.SyncStatus = @error) ORDER BY m.AddedAt",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@pending", (int)SyncStatus.Pending);
                    cmd.Parameters.AddWithValue("@error", (int)SyncStatus.Error);
                    cmd.Parameters.AddWithValue("@includeErrors", includeErrors);
                });

        /// <summary>
        /// Records the result of sending a material to Disk.
        /// The status is kept as pending if the material changed while it was being sent.
        /// </summary>
        public async Task UpdateSyncStateAsync(Material item, string remotePath, SyncStatus status, string error)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Material SET RemotePath = @RemotePath WHERE ID = @ID;
                UPDATE Material SET SyncStatus = @SyncStatus, SyncError = @SyncError
                WHERE ID = @ID AND SyncVersion = @SyncVersion;";
            updateCmd.Parameters.AddWithValue("@ID", item.ID);
            updateCmd.Parameters.AddWithValue("@RemotePath", remotePath);
            updateCmd.Parameters.AddWithValue("@SyncStatus", (int)status);
            updateCmd.Parameters.AddWithValue("@SyncError", error);
            updateCmd.Parameters.AddWithValue("@SyncVersion", item.SyncVersion);

            await updateCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Counts materials whose last upload to Disk failed.
        /// </summary>
        public async Task<int> CountSyncErrorsAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Material WHERE SyncStatus = @error";
            countCmd.Parameters.AddWithValue("@error", (int)SyncStatus.Error);

            return Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// Marks all materials of a subject for sending again, e.g. after the subject was renamed.
        /// </summary>
        public async Task MarkSubjectPendingAsync(int subjectId)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE Material SET SyncStatus = 0, SyncVersion = SyncVersion + 1 WHERE SubjectID = @subjectId";
            updateCmd.Parameters.AddWithValue("@subjectId", subjectId);

            await updateCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Counts materials per subject.
        /// </summary>
        /// <returns>Material count by subject ID.</returns>
        public async Task<Dictionary<int, int>> CountBySubjectAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT SubjectID, COUNT(*) FROM Material GROUP BY SubjectID";
            var counts = new Dictionary<int, int>();

            await using var reader = await countCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                counts[reader.GetInt32(0)] = reader.GetInt32(1);
            }

            return counts;
        }

        /// <summary>
        /// Saves a material to the database. If the material ID is 0, a new material is created; otherwise, the existing material is updated.
        /// </summary>
        /// <param name="item">The material to save.</param>
        /// <returns>The ID of the saved material.</returns>
        public async Task<int> SaveItemAsync(Material item)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var saveCmd = connection.CreateCommand();
            if (item.ID == 0)
            {
                saveCmd.CommandText = @"
                INSERT INTO Material (SubjectID, Title, Kind, FilePath, Url, SizeBytes, AddedAt)
                VALUES (@SubjectID, @Title, @Kind, @FilePath, @Url, @SizeBytes, @AddedAt);
                SELECT last_insert_rowid();";
            }
            else
            {
                // Any change has to reach Disk too, so the material goes back to pending.
                saveCmd.CommandText = @"
                UPDATE Material
                SET SubjectID = @SubjectID, Title = @Title, Kind = @Kind, FilePath = @FilePath,
                    Url = @Url, SizeBytes = @SizeBytes, AddedAt = @AddedAt,
                    SyncStatus = 0, SyncVersion = SyncVersion + 1
                WHERE ID = @ID";
                saveCmd.Parameters.AddWithValue("@ID", item.ID);
            }

            saveCmd.Parameters.AddWithValue("@SubjectID", item.SubjectID);
            saveCmd.Parameters.AddWithValue("@Title", item.Title);
            saveCmd.Parameters.AddWithValue("@Kind", (int)item.Kind);
            saveCmd.Parameters.AddWithValue("@FilePath", item.FilePath);
            saveCmd.Parameters.AddWithValue("@Url", item.Url);
            saveCmd.Parameters.AddWithValue("@SizeBytes", item.SizeBytes);
            // ISO 8601 in UTC sorts correctly as text, so ORDER BY AddedAt works.
            saveCmd.Parameters.AddWithValue("@AddedAt", item.AddedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));

            var result = await saveCmd.ExecuteScalarAsync();
            if (item.ID == 0)
            {
                item.ID = Convert.ToInt32(result);
            }

            return item.ID;
        }

        /// <summary>
        /// Deletes a material record from the database and queues its copy on Disk for deletion.
        /// </summary>
        /// <param name="item">The material to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(Material item)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = @"
            INSERT INTO RemoteDeletion (Path) SELECT RemotePath FROM Material WHERE ID = @id AND RemotePath <> '';
            DELETE FROM Material WHERE ID = @id;";
            deleteCmd.Parameters.AddWithValue("@id", item.ID);

            return await deleteCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Deletes all material records of a specific subject and queues their copies on Disk for deletion.
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
            INSERT INTO RemoteDeletion (Path) SELECT RemotePath FROM Material WHERE SubjectID = @subjectId AND RemotePath <> '';
            DELETE FROM Material WHERE SubjectID = @subjectId;";
            deleteCmd.Parameters.AddWithValue("@subjectId", subjectId);

            return await deleteCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Drops the Material table from the database.
        /// </summary>
        public async Task DropTableAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var dropTableCmd = connection.CreateCommand();
            dropTableCmd.CommandText = "DROP TABLE IF EXISTS Material";
            await dropTableCmd.ExecuteNonQueryAsync();
            _hasBeenInitialized = false;
        }

        private async Task<List<Material>> QueryAsync(string sql, Action<SqliteCommand>? addParameters = null)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = sql;
            addParameters?.Invoke(selectCmd);
            var materials = new List<Material>();

            await using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                materials.Add(new Material
                {
                    ID = reader.GetInt32(0),
                    SubjectID = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Kind = (MaterialKind)reader.GetInt32(3),
                    FilePath = reader.GetString(4),
                    Url = reader.GetString(5),
                    SizeBytes = reader.GetInt64(6),
                    AddedAt = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    SubjectName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    SubjectColor = reader.IsDBNull(9) ? SubjectPalette.DefaultColor : reader.GetString(9),
                    RemotePath = reader.GetString(10),
                    SyncStatus = (SyncStatus)reader.GetInt32(11),
                    SyncError = reader.GetString(12),
                    SyncVersion = reader.GetInt32(13)
                });
            }

            return materials;
        }
    }
}
