using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    public partial class SubjectDetailPageModel : ObservableObject, IQueryAttributable, ITaskPageModel, IMaterialPageModel,
        INotePageModel, IRecipient<NotesChangedMessage>, IRecipient<CloudSyncFinishedMessage>
    {
        private Subject? _subject;
        private readonly SubjectRepository _subjectRepository;
        private readonly TaskRepository _taskRepository;
        private readonly TagRepository _tagRepository;
        private readonly MaterialRepository _materialRepository;
        private readonly NoteRepository _noteRepository;
        private readonly MaterialService _materialService;
        private readonly ModalErrorHandler _errorHandler;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _teacher = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private List<StudyTask> _tasks = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoMaterials))]
        private List<Material> _materials = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoNotes))]
        private List<Note> _notes = [];

        [ObservableProperty]
        private List<Tag> _allTags = [];

        public IList<object> SelectedTags { get; set; } = new List<object>();

        [ObservableProperty]
        private IconData _icon;

        [ObservableProperty]
        private ColorData _color;

        [ObservableProperty]
        bool _isBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNew))]
        [NotifyPropertyChangedFor(nameof(HasNoMaterials))]
        [NotifyPropertyChangedFor(nameof(HasNoNotes))]
        private bool _isExisting;

        public IReadOnlyList<IconData> IconOptions => SubjectPalette.IconOptions;

        public IReadOnlyList<ColorData> ColorOptions => SubjectPalette.ColorOptions;

        public bool IsNew => !IsExisting;

        public bool HasNoMaterials => IsExisting && Materials.Count == 0;

        public bool HasNoNotes => IsExisting && Notes.Count == 0;

        private bool _canDelete;

        public bool CanDelete
        {
            get => _canDelete;
            set
            {
                _canDelete = value;
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }

        public bool HasCompletedTasks
            => _subject?.Tasks.Any(t => t.IsCompleted) ?? false;

        public SubjectDetailPageModel(SubjectRepository subjectRepository, TaskRepository taskRepository, TagRepository tagRepository,
            MaterialRepository materialRepository, NoteRepository noteRepository, MaterialService materialService, ModalErrorHandler errorHandler)
        {
            _subjectRepository = subjectRepository;
            _taskRepository = taskRepository;
            _tagRepository = tagRepository;
            _materialRepository = materialRepository;
            _noteRepository = noteRepository;
            _materialService = materialService;
            _errorHandler = errorHandler;
            _icon = IconOptions[0];
            _color = ColorOptions[0];
            Tasks = [];
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public void Receive(NotesChangedMessage message) =>
            LoadNotes().FireAndForgetSafeAsync(_errorHandler);

        // Refreshes the cloud icons once a backup run has finished.
        public void Receive(CloudSyncFinishedMessage message) =>
            Task.WhenAll(LoadMaterials(), LoadNotes()).FireAndForgetSafeAsync(_errorHandler);

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("id"))
            {
                int id = Convert.ToInt32(query["id"]);
                LoadData(id).FireAndForgetSafeAsync(_errorHandler);
            }
            else if (query.ContainsKey("refresh"))
            {
                RefreshData().FireAndForgetSafeAsync(_errorHandler);
            }
            else
            {
                LoadTags().FireAndForgetSafeAsync(_errorHandler);
                _subject = new();
                _subject.Tags = [];
                _subject.Tasks = [];
                Tasks = _subject.Tasks;
            }
        }

        private async Task LoadTags() =>
            AllTags = await _tagRepository.ListAsync();

        private async Task LoadMaterials()
        {
            if (!_subject.IsNullOrNew())
                Materials = await _materialRepository.ListAsync(_subject.ID);
        }

        private async Task LoadNotes()
        {
            if (!_subject.IsNullOrNew())
                Notes = await _noteRepository.ListAsync(_subject.ID);
        }

        private async Task RefreshData()
        {
            if (_subject.IsNullOrNew())
            {
                if (_subject is not null)
                    Tasks = new(_subject.Tasks);

                return;
            }

            Tasks = await _taskRepository.ListAsync(_subject.ID);
            _subject.Tasks = Tasks;
            OnPropertyChanged(nameof(HasCompletedTasks));
        }

        private async Task LoadData(int id)
        {
            try
            {
                IsBusy = true;

                _subject = await _subjectRepository.GetAsync(id);

                if (_subject.IsNullOrNew())
                {
                    _errorHandler.HandleError(new Exception(string.Format(AppResources.SubjectNotFound, id)));
                    return;
                }

                Name = _subject.Name;
                Teacher = _subject.Teacher;
                Description = _subject.Description;
                Tasks = _subject.Tasks;
                Icon = IconOptions.FirstOrDefault(i => i.Icon == _subject.Icon) ?? IconOptions[0];
                Color = ColorOptions.FirstOrDefault(c => string.Equals(c.Hex, _subject.Color, StringComparison.OrdinalIgnoreCase)) ?? ColorOptions[0];
                IsExisting = true;

                await LoadMaterials();
                await LoadNotes();

                var allTags = await _tagRepository.ListAsync();
                foreach (var tag in allTags)
                {
                    tag.IsSelected = _subject.Tags.Any(t => t.ID == tag.ID);
                    if (tag.IsSelected)
                    {
                        SelectedTags.Add(tag);
                    }
                }
                AllTags = new(allTags);
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
            finally
            {
                IsBusy = false;
                CanDelete = !_subject.IsNullOrNew();
                OnPropertyChanged(nameof(HasCompletedTasks));
            }
        }

        [RelayCommand]
        private async Task TaskCompleted(StudyTask task)
        {
            await _taskRepository.SaveItemAsync(task);
            OnPropertyChanged(nameof(HasCompletedTasks));
        }

        [RelayCommand]
        private async Task Save()
        {
            if (_subject is null)
            {
                _errorHandler.HandleError(new Exception(AppResources.SubjectNotLoaded));
                return;
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                await Shell.Current.DisplayAlertAsync(AppResources.SubjectTitle, AppResources.SubjectNameRequired, AppResources.Ok);
                return;
            }

            bool isNew = _subject.IsNullOrNew();
            bool renamed = !isNew && _subject.Name != Name.Trim();

            _subject.Name = Name.Trim();
            _subject.Teacher = Teacher.Trim();
            _subject.Description = Description.Trim();
            _subject.Icon = Icon.Icon ?? FluentUI.book_24_regular;
            _subject.Color = Color.Hex;
            await _subjectRepository.SaveItemAsync(_subject);

            foreach (var tag in AllTags)
            {
                if (tag.IsSelected)
                {
                    await _tagRepository.SaveItemAsync(tag, _subject.ID);
                }
            }

            foreach (var task in _subject.Tasks)
            {
                if (task.ID == 0)
                {
                    task.SubjectID = _subject.ID;
                    await _taskRepository.SaveItemAsync(task);
                }
            }

            // Files on Disk live in a folder named after the subject, so they have to move.
            if (renamed)
            {
                await _materialRepository.MarkSubjectPendingAsync(_subject.ID);
                await _noteRepository.MarkSubjectPendingAsync(_subject.ID);
                WeakReferenceMessenger.Default.Send(new MaterialsChangedMessage());
            }

            if (isNew)
            {
                // Stay on the page: materials can only be attached to a saved subject.
                IsExisting = true;
                CanDelete = true;
                await AppShell.DisplayToastAsync(AppResources.SubjectCreated);
                return;
            }

            await Shell.Current.GoToAsync("..");
            await AppShell.DisplayToastAsync(AppResources.SubjectSaved);
        }

        [RelayCommand]
        private async Task AddTask()
        {
            if (_subject is null)
            {
                _errorHandler.HandleError(new Exception(AppResources.SubjectNotLoaded));
                return;
            }

            // Pass the subject so if this is a new subject we can just add
            // the tasks to the subject and then save them all from here.
            await Shell.Current.GoToAsync($"task",
                new ShellNavigationQueryParameters(){
                    {TaskDetailPageModel.SubjectQueryKey, _subject}
                });
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete()
        {
            if (_subject.IsNullOrNew())
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            bool confirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.DeleteSubjectTitle,
                string.Format(AppResources.DeleteSubjectMessage, _subject.Name),
                AppResources.Delete, AppResources.Cancel);
            if (!confirmed)
                return;

            await _subjectRepository.DeleteItemAsync(_subject);
            _materialService.DeleteSubjectFiles(_subject.ID);
            WeakReferenceMessenger.Default.Send(new MaterialsChangedMessage());
            await Shell.Current.GoToAsync("..");
            await AppShell.DisplayToastAsync(AppResources.SubjectDeleted);
        }

        [RelayCommand]
        private async Task AddMaterial()
        {
            if (_subject.IsNullOrNew())
                return;

            try
            {
                int added = await _materialService.AddWithPromptAsync(_subject.ID);
                if (added == 0)
                    return;

                await LoadMaterials();
                await AppShell.DisplayToastAsync(string.Format(AppResources.MaterialsAdded, added));
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }

        [RelayCommand]
        private async Task OpenMaterial(Material material)
        {
            try
            {
                await _materialService.OpenAsync(material);
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }

        [RelayCommand]
        private async Task MaterialActions(Material material)
        {
            try
            {
                if (await _materialService.ShowActionsAsync(material))
                    await LoadMaterials();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }

        [RelayCommand]
        private Task AddNote() =>
            _subject.IsNullOrNew()
                ? Task.CompletedTask
                : Shell.Current.GoToAsync($"note?{NoteDetailPageModel.SubjectIdQueryKey}={_subject.ID}");

        [RelayCommand]
        private Task OpenNote(Note note) =>
            Shell.Current.GoToAsync($"note?id={note.ID}");

        [RelayCommand]
        private Task NavigateToTask(StudyTask task) =>
            Shell.Current.GoToAsync($"task?id={task.ID}");

        [RelayCommand]
        private void IconSelected(IconData icon)
        {
            SemanticScreenReader.Announce(string.Format(AppResources.ItemSelected, icon.Description));
        }

        [RelayCommand]
        private void ColorSelected(ColorData color)
        {
            SemanticScreenReader.Announce(string.Format(AppResources.ItemSelected, color.Description));
        }

        [RelayCommand]
        private async Task CleanTasks()
        {
            var completedTasks = Tasks.Where(t => t.IsCompleted).ToArray();
            foreach (var task in completedTasks)
            {
                await _taskRepository.DeleteItemAsync(task);
                Tasks.Remove(task);
            }

            Tasks = new(Tasks);
            OnPropertyChanged(nameof(HasCompletedTasks));
            await AppShell.DisplayToastAsync(AppResources.CleanedUp);
        }

        [RelayCommand]
        private async Task SelectionChanged(object parameter)
        {
            if (parameter is IEnumerable<object> enumerableParameter)
            {
                var currentSelection = enumerableParameter.OfType<Tag>().ToList();
                var previousSelection = AllTags.Where(t => t.IsSelected).ToList();

                // Handle newly selected tags
                foreach (var tag in currentSelection.Except(previousSelection))
                {
                    tag.IsSelected = true;
                    if (!_subject.IsNullOrNew())
                    {
                        await _tagRepository.SaveItemAsync(tag, _subject.ID);
                    }
                }

                // Handle deselected tags
                foreach (var tag in previousSelection.Except(currentSelection))
                {
                    tag.IsSelected = false;
                    if (!_subject.IsNullOrNew())
                    {
                        await _tagRepository.DeleteItemAsync(tag, _subject.ID);
                    }
                }
            }
        }
    }
}
