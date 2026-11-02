using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StudyFlow.Models;

namespace StudyFlow.Data
{
    /// <summary>
    /// Repository class for managing subjects in the database.
    /// </summary>
    public class SubjectRepository
    {
        internal const string CreateTableSql = @"
            CREATE TABLE IF NOT EXISTS Subject (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Teacher TEXT NOT NULL,
                Description TEXT NOT NULL,
                Icon TEXT NOT NULL,
                Color TEXT NOT NULL
            );";

        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;
        private readonly TaskRepository _taskRepository;
        private readonly TagRepository _tagRepository;
        private readonly MaterialRepository _materialRepository;
        private readonly NoteRepository _noteRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubjectRepository"/> class.
        /// </summary>
        /// <param name="taskRepository">The task repository instance.</param>
        /// <param name="tagRepository">The tag repository instance.</param>
        /// <param name="materialRepository">The material repository instance.</param>
        /// <param name="noteRepository">The note repository instance.</param>
        /// <param name="logger">The logger instance.</param>
        public SubjectRepository(TaskRepository taskRepository, TagRepository tagRepository, MaterialRepository materialRepository,
            NoteRepository noteRepository, ILogger<SubjectRepository> logger)
        {
            _taskRepository = taskRepository;
            _tagRepository = tagRepository;
            _materialRepository = materialRepository;
            _noteRepository = noteRepository;
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database connection and creates the Subject table if it does not exist.
        /// </summary>
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
                _logger.LogError(e, "Error creating Subject table");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves a list of all subjects with their tags, tasks, and material and note counts.
        /// </summary>
        /// <returns>A list of <see cref="Subject"/> objects.</returns>
        public async Task<List<Subject>> ListAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT ID, Name, Teacher, Description, Icon, Color FROM Subject";
            var subjects = new List<Subject>();

            await using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                subjects.Add(ReadSubject(reader));
            }

            var materialCounts = await _materialRepository.CountBySubjectAsync();
            var noteCounts = await _noteRepository.CountBySubjectAsync();
            foreach (var subject in subjects)
            {
                subject.Tags = await _tagRepository.ListAsync(subject.ID);
                subject.Tasks = await _taskRepository.ListAsync(subject.ID);
                subject.MaterialCount = materialCounts.GetValueOrDefault(subject.ID);
                subject.NoteCount = noteCounts.GetValueOrDefault(subject.ID);
            }

            return subjects;
        }

        /// <summary>
        /// Retrieves a specific subject by its ID.
        /// </summary>
        /// <param name="id">The ID of the subject.</param>
        /// <returns>A <see cref="Subject"/> object if found; otherwise, null.</returns>
        public async Task<Subject?> GetAsync(int id)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT ID, Name, Teacher, Description, Icon, Color FROM Subject WHERE ID = @id";
            selectCmd.Parameters.AddWithValue("@id", id);

            await using var reader = await selectCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var subject = ReadSubject(reader);

                subject.Tags = await _tagRepository.ListAsync(subject.ID);
                subject.Tasks = await _taskRepository.ListAsync(subject.ID);

                return subject;
            }

            return null;
        }

        /// <summary>
        /// Saves a subject to the database. If the subject ID is 0, a new subject is created; otherwise, the existing subject is updated.
        /// </summary>
        /// <param name="item">The subject to save.</param>
        /// <returns>The ID of the saved subject.</returns>
        public async Task<int> SaveItemAsync(Subject item)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var saveCmd = connection.CreateCommand();
            if (item.ID == 0)
            {
                saveCmd.CommandText = @"
                INSERT INTO Subject (Name, Teacher, Description, Icon, Color)
                VALUES (@Name, @Teacher, @Description, @Icon, @Color);
                SELECT last_insert_rowid();";
            }
            else
            {
                saveCmd.CommandText = @"
                UPDATE Subject
                SET Name = @Name, Teacher = @Teacher, Description = @Description, Icon = @Icon, Color = @Color
                WHERE ID = @ID";
                saveCmd.Parameters.AddWithValue("@ID", item.ID);
            }

            saveCmd.Parameters.AddWithValue("@Name", item.Name);
            saveCmd.Parameters.AddWithValue("@Teacher", item.Teacher);
            saveCmd.Parameters.AddWithValue("@Description", item.Description);
            saveCmd.Parameters.AddWithValue("@Icon", item.Icon);
            saveCmd.Parameters.AddWithValue("@Color", item.Color);

            var result = await saveCmd.ExecuteScalarAsync();
            if (item.ID == 0)
            {
                item.ID = Convert.ToInt32(result);
            }

            return item.ID;
        }

        /// <summary>
        /// Deletes a subject together with its tasks, notes, tag links and material records.
        /// Material files are removed separately by <see cref="MaterialService.DeleteSubjectFiles"/>.
        /// </summary>
        /// <param name="item">The subject to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(Subject item)
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Subject WHERE ID = @ID";
            deleteCmd.Parameters.AddWithValue("@ID", item.ID);
            var deleted = await deleteCmd.ExecuteNonQueryAsync();

            await _taskRepository.DeleteBySubjectAsync(item.ID);
            await _noteRepository.DeleteBySubjectAsync(item.ID);
            await _tagRepository.DeleteLinksAsync(item.ID);
            await _materialRepository.DeleteBySubjectAsync(item.ID);

            return deleted;
        }

        /// <summary>
        /// Drops the Subject table and all tables that depend on it.
        /// </summary>
        public async Task DropTableAsync()
        {
            await Init();
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();

            var dropCmd = connection.CreateCommand();
            dropCmd.CommandText = "DROP TABLE IF EXISTS Subject";
            await dropCmd.ExecuteNonQueryAsync();

            await _taskRepository.DropTableAsync();
            await _tagRepository.DropTableAsync();
            await _materialRepository.DropTableAsync();
            await _noteRepository.DropTableAsync();
            _hasBeenInitialized = false;
        }

        private static Subject ReadSubject(SqliteDataReader reader) => new()
        {
            ID = reader.GetInt32(0),
            Name = reader.GetString(1),
            Teacher = reader.GetString(2),
            Description = reader.GetString(3),
            Icon = reader.GetString(4),
            Color = reader.GetString(5)
        };
    }
}
