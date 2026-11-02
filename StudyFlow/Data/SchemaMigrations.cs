using Microsoft.Data.Sqlite;

namespace StudyFlow.Data
{
    /// <summary>
    /// Brings tables created by earlier versions of the app up to date.
    /// CREATE TABLE IF NOT EXISTS does not add new columns to an existing table.
    /// </summary>
    internal static class SchemaMigrations
    {
        /// <summary>
        /// Adds the Yandex Disk sync columns (added in the cloud backup version) to Material or Note.
        /// </summary>
        public static async Task AddSyncColumnsAsync(SqliteConnection connection, string table)
        {
            await AddColumnIfMissingAsync(connection, table, "RemotePath", "TEXT NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, table, "SyncStatus", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync(connection, table, "SyncError", "TEXT NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, table, "SyncVersion", "INTEGER NOT NULL DEFAULT 0");
        }

        private static async Task AddColumnIfMissingAsync(SqliteConnection connection, string table, string column, string definition)
        {
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @column";
            checkCmd.Parameters.AddWithValue("@column", column);

            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                return;

            var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            await alterCmd.ExecuteNonQueryAsync();
        }
    }
}
