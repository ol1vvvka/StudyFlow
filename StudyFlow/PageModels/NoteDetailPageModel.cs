using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    /// <summary>
    /// Edits a note. There is no Save button: changes are saved shortly after typing stops and when leaving the page.
    /// </summary>
    public partial class NoteDetailPageModel : ObservableObject, IQueryAttributable
    {
        public const string SubjectIdQueryKey = "subjectId";
        private static readonly TimeSpan AutosaveDelay = TimeSpan.FromSeconds(1.5);

        private Note? _note;
        private bool _isLoading;
        private bool _isDirty;
        private bool _isDeleted;
        private bool _canDelete;
        private CancellationTokenSource? _autosaveCts;
        // Autosave and save-on-leave can overlap; without the lock a new note could be inserted twice.
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly NoteRepository _noteRepository;
        private readonly SubjectRepository _subjectRepository;
        private readonly ModalErrorHandler _errorHandler;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private List<Subject> _subjects = [];

        [ObservableProperty]
        private Subject? _subject;

        [ObservableProperty]
        private string _status = string.Empty;

        public NoteDetailPageModel(NoteRepository noteRepository, SubjectRepository subjectRepository, ModalErrorHandler errorHandler)
        {
            _noteRepository = noteRepository;
            _subjectRepository = subjectRepository;
            _errorHandler = errorHandler;
        }

        public bool CanDelete
        {
            get => _canDelete;
            set
            {
                _canDelete = value;
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            LoadAsync(query).FireAndForgetSafeAsync(_errorHandler);
        }

        private async Task LoadAsync(IDictionary<string, object> query)
        {
            _isLoading = true;
            try
            {
                var subjects = await _subjectRepository.ListAsync();
                subjects.Insert(0, new Subject { ID = 0, Name = AppResources.NoSubject });
                Subjects = subjects;

                if (query.ContainsKey("id"))
                {
                    int id = Convert.ToInt32(query["id"]);
                    _note = await _noteRepository.GetAsync(id);

                    if (_note is null)
                    {
                        _errorHandler.HandleError(new Exception(string.Format(AppResources.NoteNotFound, id)));
                        return;
                    }

                    Title = _note.Title;
                    Content = _note.Content;
                    Status = string.Format(AppResources.NoteSavedAt, _note.UpdatedAt.ToLocalTime());
                    CanDelete = true;
                }
                else
                {
                    int subjectId = query.TryGetValue(SubjectIdQueryKey, out var value) ? Convert.ToInt32(value) : 0;
                    _note = new Note { SubjectID = subjectId };
                }

                Subject = Subjects.FirstOrDefault(s => s.ID == _note.SubjectID) ?? Subjects[0];
            }
            finally
            {
                _isLoading = false;
            }
        }

        partial void OnTitleChanged(string value) => ScheduleAutosave();

        partial void OnContentChanged(string value) => ScheduleAutosave();

        partial void OnSubjectChanged(Subject? value)
        {
            // The picker briefly reports null while its items are replaced.
            if (value is not null)
                ScheduleAutosave();
        }

        private void ScheduleAutosave()
        {
            if (_isLoading || _note is null)
                return;

            _isDirty = true;
            _autosaveCts?.Cancel();
            _autosaveCts = new CancellationTokenSource();
            AutosaveAfterDelayAsync(_autosaveCts.Token).FireAndForgetSafeAsync(_errorHandler);
        }

        private async Task AutosaveAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(AutosaveDelay, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await SaveIfDirtyAsync();
        }

        private async Task SaveIfDirtyAsync()
        {
            await _saveLock.WaitAsync();
            try
            {
                if (!_isDirty || _isDeleted || _note is null)
                    return;

                var title = Title.Trim();
                var content = NormalizeNewLines(Content).TrimEnd();
                var subjectId = Subject?.ID ?? 0;

                // A new note the user has not typed anything into is not worth keeping.
                if (_note.ID == 0 && title.Length == 0 && content.Length == 0)
                    return;

                _isDirty = false;

                // The editor can rewrite line breaks right after loading; that alone must not move the note to the top.
                if (_note.ID > 0 && title == _note.Title && content == _note.Content && subjectId == _note.SubjectID)
                    return;

                var now = DateTime.UtcNow;
                if (_note.ID == 0)
                    _note.CreatedAt = now;
                _note.UpdatedAt = now;
                _note.Title = title;
                _note.Content = content;
                _note.SubjectID = subjectId;

                await _noteRepository.SaveItemAsync(_note);

                CanDelete = true;
                Status = string.Format(AppResources.NoteSavedAt, now.ToLocalTime());
                WeakReferenceMessenger.Default.Send(new NotesChangedMessage());
            }
            finally
            {
                _saveLock.Release();
            }
        }

        // WinUI's text box uses "\r" for line breaks, Android uses "\n"; store "\n" everywhere.
        private static string NormalizeNewLines(string text) =>
            text.Replace("\r\n", "\n").Replace('\r', '\n');

        [RelayCommand]
        private async Task NavigatedFrom()
        {
            _autosaveCts?.Cancel();
            try
            {
                await SaveIfDirtyAsync();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }

        [RelayCommand]
        private Task Done() => Shell.Current.GoToAsync("..");

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete()
        {
            if (_note is null)
                return;

            bool confirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.DeleteNoteTitle,
                string.Format(AppResources.DeleteNoteMessage, _note.DisplayTitle),
                AppResources.Delete, AppResources.Cancel);
            if (!confirmed)
                return;

            _isDeleted = true;
            _autosaveCts?.Cancel();

            await _saveLock.WaitAsync();
            try
            {
                if (_note.ID > 0)
                    await _noteRepository.DeleteItemAsync(_note);
            }
            finally
            {
                _saveLock.Release();
            }

            WeakReferenceMessenger.Default.Send(new NotesChangedMessage());
            await Shell.Current.GoToAsync("..");
            await AppShell.DisplayToastAsync(AppResources.NoteDeleted);
        }
    }
}
