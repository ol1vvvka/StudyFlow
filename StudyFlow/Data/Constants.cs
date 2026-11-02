namespace StudyFlow.Data
{
    public static class Constants
    {
        // Not the template's AppSQLite.db3: its Task table has a ProjectID column that CREATE TABLE IF NOT EXISTS would keep.
        public const string DatabaseFilename = "StudyFlow.db3";

        public static string DatabasePath =>
            $"Data Source={Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename)}";

        public static string MaterialsDirectory =>
            Path.Combine(FileSystem.AppDataDirectory, "materials");
    }
}
