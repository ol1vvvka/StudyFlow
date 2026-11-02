using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    public partial class NoteListPageModel : ObservableObject, INotePageModel,
        IRecipient<NotesChangedMessage>, IRecipient<CloudSyncFinishedMessage>
    {
        private readonly NoteRepository _noteRepository;
        private readonly ModalErrorHandler _errorHandler;
        private List<Note> _allNotes = [];

        [ObservableProperty]
        private List<Note> _notes = [];

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _emptyText = AppResources.NotesAllEmpty;

        public NoteListPageModel(NoteRepository noteRepository, ModalErrorHandler errorHandler)
        {
            _noteRepository = noteRepository;
            _errorHandler = errorHandler;
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public void Receive(NotesChangedMessage message) =>
            LoadData().FireAndForgetSafeAsync(_errorHandler);

        public void Receive(CloudSyncFinishedMessage message) =>
            LoadData().FireAndForgetSafeAsync(_errorHandler);

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var query = SearchText.Trim();

            Notes = query.Length == 0
                ? _allNotes
                : _allNotes
                    .Where(n => n.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                        || n.Content.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                        || n.SubjectName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

            EmptyText = _allNotes.Count == 0 ? AppResources.NotesAllEmpty : AppResources.NotesNotFound;
        }

        private async Task LoadData()
        {
            _allNotes = await _noteRepository.ListAsync();
            ApplyFilter();
        }

        [RelayCommand]
        private async Task Appearing()
        {
            try
            {
                await LoadData();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }

        [RelayCommand]
        private Task OpenNote(Note note)
            => Shell.Current.GoToAsync($"note?id={note.ID}");

        [RelayCommand]
        private Task AddNote()
            => Shell.Current.GoToAsync("note");
    }
}
