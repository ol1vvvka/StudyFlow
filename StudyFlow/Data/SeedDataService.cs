using Microsoft.Extensions.Logging;
using StudyFlow.Models;
using System.Text.Json;

namespace StudyFlow.Data
{
    public class SeedDataService
    {
        private readonly SubjectRepository _subjectRepository;
        private readonly TaskRepository _taskRepository;
        private readonly TagRepository _tagRepository;
        private readonly NoteRepository _noteRepository;
        private readonly string _seedDataFilePath = "SeedData.json";
        private readonly ILogger<SeedDataService> _logger;

        public SeedDataService(SubjectRepository subjectRepository, TaskRepository taskRepository, TagRepository tagRepository,
            NoteRepository noteRepository, ILogger<SeedDataService> logger)
        {
            _subjectRepository = subjectRepository;
            _taskRepository = taskRepository;
            _tagRepository = tagRepository;
            _noteRepository = noteRepository;
            _logger = logger;
        }

        public async Task LoadSeedDataAsync()
        {
            await ClearTablesAsync();

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
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error saving seed data");
                throw;
            }
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
    }
}
