using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    public partial class TaskDetailPageModel : ObservableObject, IQueryAttributable
    {
        public const string SubjectQueryKey = "subject";
        private StudyTask? _task;
        private bool _canDelete;
        private readonly SubjectRepository _subjectRepository;
        private readonly TaskRepository _taskRepository;
        private readonly ModalErrorHandler _errorHandler;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private List<Subject> _subjects = [];

        [ObservableProperty]
        private Subject? _subject;

        [ObservableProperty]
        private int _selectedSubjectIndex = -1;

        [ObservableProperty]
        private bool _isExistingSubject;

        public TaskDetailPageModel(SubjectRepository subjectRepository, TaskRepository taskRepository, ModalErrorHandler errorHandler)
        {
            _subjectRepository = subjectRepository;
            _taskRepository = taskRepository;
            _errorHandler = errorHandler;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            LoadTaskAsync(query).FireAndForgetSafeAsync(_errorHandler);
        }

        private async Task LoadTaskAsync(IDictionary<string, object> query)
        {
            if (query.TryGetValue(SubjectQueryKey, out var subject))
                Subject = (Subject)subject;

            int taskId = 0;

            if (query.ContainsKey("id"))
            {
                taskId = Convert.ToInt32(query["id"]);
                _task = await _taskRepository.GetAsync(taskId);

                if (_task is null)
                {
                    _errorHandler.HandleError(new Exception(string.Format(AppResources.TaskNotFound, taskId)));
                    return;
                }

                Subject = await _subjectRepository.GetAsync(_task.SubjectID);
            }
            else
            {
                _task = new StudyTask();
            }

            // If the subject is new, we don't need to load the subject dropdown
            if (Subject?.ID == 0)
            {
                IsExistingSubject = false;
            }
            else
            {
                Subjects = await _subjectRepository.ListAsync();
                IsExistingSubject = true;
            }

            if (Subject is not null)
                SelectedSubjectIndex = Subjects.FindIndex(s => s.ID == Subject.ID);
            else if (_task?.SubjectID > 0)
                SelectedSubjectIndex = Subjects.FindIndex(s => s.ID == _task.SubjectID);

            if (taskId > 0)
            {
                if (_task is null)
                {
                    _errorHandler.HandleError(new Exception(string.Format(AppResources.TaskNotFound, taskId)));
                    return;
                }

                Title = _task.Title;
                IsCompleted = _task.IsCompleted;
                CanDelete = true;
            }
            else
            {
                _task = new StudyTask()
                {
                    SubjectID = Subject?.ID ?? 0
                };
            }
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

        [RelayCommand]
        private async Task Save()
        {
            if (_task is null)
            {
                _errorHandler.HandleError(new Exception(AppResources.TaskNotLoaded));
                return;
            }

            int subjectId = Subject?.ID ?? 0;

            if (Subjects.Count > SelectedSubjectIndex && SelectedSubjectIndex >= 0)
                subjectId = Subjects[SelectedSubjectIndex].ID;

            // A new, unsaved subject has ID 0 but still owns the task in memory.
            if (Subject is null && subjectId == 0)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.TaskTitle, AppResources.TaskSubjectRequired, AppResources.Ok);
                return;
            }

            _task.Title = Title;
            _task.SubjectID = subjectId;
            _task.IsCompleted = IsCompleted;

            if (Subject?.ID == subjectId && !Subject.Tasks.Contains(_task))
                Subject.Tasks.Add(_task);

            if (_task.SubjectID > 0)
                await _taskRepository.SaveItemAsync(_task);

            await Shell.Current.GoToAsync("..?refresh=true");

            if (_task.ID > 0)
                await AppShell.DisplayToastAsync(AppResources.TaskSaved);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete()
        {
            if (_task is null || Subject is null)
            {
                _errorHandler.HandleError(new Exception(AppResources.TaskNotLoaded));
                return;
            }

            if (Subject.Tasks.Contains(_task))
                Subject.Tasks.Remove(_task);

            if (_task.ID > 0)
                await _taskRepository.DeleteItemAsync(_task);

            await Shell.Current.GoToAsync("..?refresh=true");
            await AppShell.DisplayToastAsync(AppResources.TaskDeleted);
        }
    }
}
