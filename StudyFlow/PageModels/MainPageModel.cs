using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    public partial class MainPageModel : ObservableObject, ITaskPageModel, IMaterialPageModel, IRecipient<CloudSyncFinishedMessage>
    {
        private const string SeededKey = "studyflow_seeded";
        private const int RecentMaterialsCount = 5;

        private bool _isNavigatedTo;
        private bool _dataLoaded;
        private readonly SubjectRepository _subjectRepository;
        private readonly TaskRepository _taskRepository;
        private readonly MaterialRepository _materialRepository;
        private readonly MaterialService _materialService;
        private readonly CloudBackupService _cloudBackup;
        private readonly ModalErrorHandler _errorHandler;
        private readonly SeedDataService _seedDataService;

        [ObservableProperty]
        private List<StudyTask> _tasks = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSubjects))]
        private List<Subject> _subjects = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRecentMaterials))]
        private List<Material> _recentMaterials = [];

        [ObservableProperty]
        bool _isBusy;

        [ObservableProperty]
        bool _isRefreshing;

        [ObservableProperty]
        private string _today = Formatting.CapitalizeFirst(DateTime.Now.ToString("dddd, d MMMM"));

        [ObservableProperty]
        private Subject? selectedSubject;

        public bool HasCompletedTasks
            => Tasks?.Any(t => t.IsCompleted) ?? false;

        public bool HasSubjects => Subjects.Count > 0;

        public bool HasRecentMaterials => RecentMaterials.Count > 0;

        public MainPageModel(SeedDataService seedDataService, SubjectRepository subjectRepository,
            TaskRepository taskRepository, MaterialRepository materialRepository,
            MaterialService materialService, CloudBackupService cloudBackup, ModalErrorHandler errorHandler)
        {
            _subjectRepository = subjectRepository;
            _taskRepository = taskRepository;
            _materialRepository = materialRepository;
            _materialService = materialService;
            _cloudBackup = cloudBackup;
            _errorHandler = errorHandler;
            _seedDataService = seedDataService;
            WeakReferenceMessenger.Default.Register(this);
        }

        // Refreshes the cloud icons of recent materials once a backup run has finished.
        public void Receive(CloudSyncFinishedMessage message) =>
            LoadRecentMaterials().FireAndForgetSafeAsync(_errorHandler);

        private async Task LoadRecentMaterials() =>
            RecentMaterials = await _materialRepository.ListRecentAsync(RecentMaterialsCount);

        private async Task LoadData()
        {
            try
            {
                IsBusy = true;

                Subjects = await _subjectRepository.ListAsync();
                await LoadRecentMaterials();
                Tasks = await _taskRepository.ListAsync();
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(HasCompletedTasks));
            }
        }

        private async Task InitData(SeedDataService seedDataService)
        {
            bool isSeeded = Preferences.Default.ContainsKey(SeededKey);

            if (!isSeeded)
            {
                await seedDataService.LoadSeedDataAsync();
            }

            Preferences.Default.Set(SeededKey, true);

            // Restores the Yandex session first, so lists know whether to show cloud icons.
            await _cloudBackup.InitializeAsync();
            await Refresh();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            try
            {
                IsRefreshing = true;
                await LoadData();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private void NavigatedTo() =>
            _isNavigatedTo = true;

        [RelayCommand]
        private void NavigatedFrom() =>
            _isNavigatedTo = false;

        [RelayCommand]
        private async Task Appearing()
        {
            if (!_dataLoaded)
            {
                await InitData(_seedDataService);
                _dataLoaded = true;
            }
            // This means we are being navigated to
            else if (!_isNavigatedTo)
            {
                await Refresh();
            }

            // Picks up anything left unsent last time (the app was closed or offline).
            _cloudBackup.RequestSync();
        }

        [RelayCommand]
        private Task TaskCompleted(StudyTask task)
        {
            OnPropertyChanged(nameof(HasCompletedTasks));
            return _taskRepository.SaveItemAsync(task);
        }

        [RelayCommand]
        private Task AddTask()
            => Shell.Current.GoToAsync($"task");

        [RelayCommand]
        private Task OpenSettings()
            => Shell.Current.GoToAsync("settings");

        [RelayCommand]
        private Task? NavigateToSubject(Subject subject)
            => subject is null ? null : Shell.Current.GoToAsync($"subject?id={subject.ID}");

        [RelayCommand]
        private Task NavigateToTask(StudyTask task)
            => Shell.Current.GoToAsync($"task?id={task.ID}");

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
                    await Refresh();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }

        [RelayCommand]
        private async Task CleanTasks()
        {
            var completedTasks = Tasks.Where(t => t.IsCompleted).ToList();
            foreach (var task in completedTasks)
            {
                await _taskRepository.DeleteItemAsync(task);
                Tasks.Remove(task);
            }

            OnPropertyChanged(nameof(HasCompletedTasks));
            Tasks = new(Tasks);
            await AppShell.DisplayToastAsync(AppResources.CleanedUp);
        }
    }
}
