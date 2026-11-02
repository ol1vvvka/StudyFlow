using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace StudyFlow.Data
{
    /// <summary>
    /// Paths on Yandex Disk whose local material or note was deleted and that still have to be removed there.
    /// Rows are added by <see cref="MaterialRepository"/> and <see cref="NoteRepository"/> when they delete an uploaded item.
    /// </summary>
    public class RemoteDeletionRepository
    {
        internal const string CreateTableSql = @"
            CREATE TABLE IF NOT EXISTS RemoteDeletion (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL
            );";

        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;

        public RemoteDeletionRepository(ILogger<RemoteDeletionRepository> logger)
        {
            _logger = logger;
        }

        private async Task Init()
        {
            if (_hasBeenInitialized)
                return;

            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            try
            {
                var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = CreateTableSql;
                await createTableCmd.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating RemoteDeletion table");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves the pending deletions as (ID, path on Disk).
        /// </summary>
        public async Task<List<(int Id, string Path)>> ListAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT ID, Path FROM RemoteDeletion ORDER BY ID";
            var deletions = new List<(int, string)>();

            await using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                deletions.Add((reader.GetInt32(0), reader.GetString(1)));
            }

            return deletions;
        }

        /// <summary>
        /// Forgets a deletion once it has been done on Disk.
        /// </summary>
        public async Task<int> DeleteItemAsync(int id)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM RemoteDeletion WHERE ID = @id";
            deleteCmd.Parameters.AddWithValue("@id", id);

            return await deleteCmd.ExecuteNonQueryAsync();
        }
    }
}
