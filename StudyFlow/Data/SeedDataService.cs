using Microsoft.Extensions.Logging;
using StudyFlow.Models;
using System.Text.Json;

namespace StudyFlow.Data
{
    public class SeedDataService
    {
        public const string SeededKey = "studyflow_seeded";
        public const string DemoActiveKey = "studyflow_demo_active";

        private readonly SubjectRepository _subjectRepository;
        private readonly TaskRepository _taskRepository;
        private readonly TagRepository _tagRepository;
        private readonly NoteRepository _noteRepository;
        private readonly CalendarRepository _calendarRepository;
        private readonly RemoteDeletionRepository _deletionRepository;
        private readonly string _seedDataFilePath = "SeedData.json";
        private readonly ILogger<SeedDataService> _logger;

        public SeedDataService(SubjectRepository subjectRepository, TaskRepository taskRepository, TagRepository tagRepository,
            NoteRepository noteRepository, CalendarRepository calendarRepository, RemoteDeletionRepository deletionRepository,
            ILogger<SeedDataService> logger)
        {
            _subjectRepository = subjectRepository;
            _taskRepository = taskRepository;
            _tagRepository = tagRepository;
            _noteRepository = noteRepository;
            _calendarRepository = calendarRepository;
            _deletionRepository = deletionRepository;
            _logger = logger;
        }

        public static bool IsDemoActive => Preferences.Default.Get(DemoActiveKey, false);

        public async Task LoadSeedDataAsync()
        {
            await ClearTablesAsync();
            DeleteMaterialFiles();

            await using Stream templateStream = await FileSystem.OpenAppPackageFileAsync(_seedDataFilePath);

            SubjectsJson? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize(templateStream, JsonContext.Default.SubjectsJson);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error deserializing seed data");
            }

            try
            {
                if (payload is not null)
                {
                    // Several subjects can share a tag: save each title once and reuse its ID.
                    var savedTags = new Dictionary<string, Tag>();

                    foreach (var subject in payload.Subjects)
                    {
                        if (subject is null)
                        {
                            continue;
                        }

                        await _subjectRepository.SaveItemAsync(subject);

                        foreach (var task in subject.Tasks)
                        {
                            task.SubjectID = subject.ID;
                            await _taskRepository.SaveItemAsync(task);
                        }

                        foreach (var note in subject.Notes)
                        {
                            note.SubjectID = subject.ID;
                            note.CreatedAt = note.UpdatedAt = DateTime.UtcNow;
                            await _noteRepository.SaveItemAsync(note);
                        }

                        foreach (var tag in subject.Tags)
                        {
                            if (savedTags.TryGetValue(tag.Title, out var savedTag))
                            {
                                tag.ID = savedTag.ID;
                            }
                            else
                            {
                                savedTags[tag.Title] = tag;
                            }

                            await _tagRepository.SaveItemAsync(tag, subject.ID);
                        }
                    }
                }

                Preferences.Default.Set(SeededKey, true);
                Preferences.Default.Set(DemoActiveKey, true);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error saving seed data");
                throw;
            }
        }

        /// <summary>
        /// Removes all local study data without loading the demo set again.
        /// </summary>
        public async Task ClearAllDataAsync()
        {
            await ClearTablesAsync();
            await _calendarRepository.ClearAllAsync();
            await _deletionRepository.DropTableAsync();
            DeleteMaterialFiles();
            Preferences.Default.Set(SeededKey, true);
            Preferences.Default.Set(DemoActiveKey, false);
        }

        private async Task ClearTablesAsync()
        {
            try
            {
                // Drops the task, tag and material tables too.
                await _subjectRepository.DropTableAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error clearing tables");
            }
        }

        private static void DeleteMaterialFiles()
        {
            if (Directory.Exists(Constants.MaterialsDirectory))
                Directory.Delete(Constants.MaterialsDirectory, recursive: true);
        }
    }
}
