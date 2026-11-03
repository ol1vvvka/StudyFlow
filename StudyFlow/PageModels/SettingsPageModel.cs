using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyFlow.Models;
using System.Collections.ObjectModel;

namespace StudyFlow.PageModels
{
    public partial class SettingsPageModel : ObservableObject, IRecipient<CloudSyncStatusMessage>
    {
        // Order of the theme segments on the settings page
        private static readonly AppTheme[] Themes = [AppTheme.Unspecified, AppTheme.Light, AppTheme.Dark];

        // Order of the storage-mode segments when signed in
        private static readonly CloudStorageMode[] StorageModes = [CloudStorageMode.DeviceAndCloud, CloudStorageMode.CloudOnly];

        private readonly TagRepository _tagRepository;
        private readonly SeedDataService _seedDataService;
        private readonly YandexAuthService _yandexAuth;
        private readonly YandexDiskClient _yandexDisk;
        private readonly CloudBackupService _cloudBackup;
        private readonly ModalErrorHandler _errorHandler;
        private CancellationTokenSource? _signInCts;

        [ObservableProperty]
        private ObservableCollection<Tag> _tags = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSignedOut))]
        private bool _isSignedIn;

        [ObservableProperty]
        private bool _isSigningIn;

        [ObservableProperty]
        private string _userName = string.Empty;

        [ObservableProperty]
        private string _userLogin = string.Empty;

        [ObservableProperty]
        private ImageSource? _avatar;

        [ObservableProperty]
        private string _diskUsage = string.Empty;

        [ObservableProperty]
        private string _syncStatusText = string.Empty;

        [ObservableProperty]
        private bool _wifiOnly;

        public SettingsPageModel(TagRepository tagRepository, SeedDataService seedDataService,
            YandexAuthService yandexAuth, YandexDiskClient yandexDisk, CloudBackupService cloudBackup, ModalErrorHandler errorHandler)
        {
            _tagRepository = tagRepository;
            _seedDataService = seedDataService;
            _yandexAuth = yandexAuth;
            _yandexDisk = yandexDisk;
            _cloudBackup = cloudBackup;
            _errorHandler = errorHandler;
            _wifiOnly = cloudBackup.WifiOnly;
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(CloudSyncStatusMessage message) =>
            MainThread.BeginInvokeOnMainThread(UpdateSyncStatus);

        partial void OnWifiOnlyChanged(bool value) => _cloudBackup.WifiOnly = value;

        private void UpdateSyncStatus()
        {
            if (_cloudBackup.IsRunning && _cloudBackup.Total > 0)
            {
                SyncStatusText = string.Format(AppResources.CloudSyncProgress, _cloudBackup.Done, _cloudBackup.Total);
                return;
            }

            var parts = new List<string>();
            if (_cloudBackup.ErrorCount > 0)
                parts.Add(string.Format(AppResources.CloudSyncFailed, _cloudBackup.ErrorCount));
            if (_cloudBackup.WaitingForWifiCount > 0)
                parts.Add(string.Format(AppResources.CloudSyncWaitingWifi, _cloudBackup.WaitingForWifiCount));
            if (parts.Count == 0)
            {
                parts.Add(_cloudBackup.LastSyncAt is { } lastSync
                    ? string.Format(AppResources.CloudSyncUpToDate, lastSync.ToLocalTime())
                    : AppResources.CloudSyncNever);
            }

            SyncStatusText = string.Join(" ", parts);
        }

        public bool IsSignedOut => !IsSignedIn;

        public int ThemeIndex => Math.Max(0, Array.IndexOf(Themes, Application.Current!.UserAppTheme));

        public int StorageModeIndex => Math.Max(0, Array.IndexOf(StorageModes, _cloudBackup.StorageMode));

        public string Version => string.Format(AppResources.AppVersionFormat,
            AppInfo.Current.VersionString, AppInfo.Current.BuildString);

        public void SetTheme(int index)
        {
            var theme = Themes[index];
            Application.Current!.UserAppTheme = theme;
            Preferences.Default.Set(App.ThemePreferenceKey, (int)theme);
#if ANDROID
            if (Platform.CurrentActivity is MainActivity activity)
                activity.UpdateSplashTheme(theme);
#endif
        }

        public void SetStorageMode(int index)
        {
            if (index < 0 || index >= StorageModes.Length)
                return;

            _cloudBackup.StorageMode = StorageModes[index];
            if (StorageModes[index] == CloudStorageMode.CloudOnly)
                _cloudBackup.RequestSync();
        }

        private async Task LoadData()
        {
            var tagsList = await _tagRepository.ListAsync();
            Tags = new ObservableCollection<Tag>(tagsList);

            await _cloudBackup.InitializeAsync();
            ShowUser(_yandexAuth.CurrentUser);
            UpdateSyncStatus();
            if (IsSignedIn)
                LoadDiskUsage().FireAndForgetSafeAsync(_errorHandler);
        }

        private void ShowUser(YandexUser? user)
        {
            IsSignedIn = user is not null;
            UserName = user?.Name ?? string.Empty;
            UserLogin = user is null ? string.Empty : string.IsNullOrEmpty(user.Email) ? user.Login : user.Email;
            Avatar = user?.AvatarUrl is { } url ? ImageSource.FromUri(new Uri(url)) : null;
            DiskUsage = string.Empty;
        }

        private async Task LoadDiskUsage()
        {
            var info = await _yandexDisk.GetDiskInfoAsync();
            if (info is null)
            {
                // The token was revoked or could not be refreshed.
                ShowUser(_yandexAuth.CurrentUser);
                return;
            }

            DiskUsage = string.Format(AppResources.YandexDiskUsage,
                Formatting.FileSize(info.UsedSpace), Formatting.FileSize(info.TotalSpace));
        }

        [RelayCommand]
        private Task Appearing()
            => LoadData();

        [RelayCommand]
        private async Task SignIn()
        {
            _signInCts = new CancellationTokenSource();
            IsSigningIn = true;
            try
            {
                var user = await _yandexAuth.SignInAsync(_signInCts.Token);
                if (user is null)
                    return;

                ShowUser(user);
                await AppShell.DisplayToastAsync(string.Format(AppResources.YandexSignedIn, user.Name));
                await LoadDiskUsage();

                if (SeedDataService.IsDemoActive)
                {
                    bool clearDemo = await Shell.Current.DisplayAlertAsync(
                        AppResources.ClearDemoOnSignInTitle, AppResources.ClearDemoOnSignInMessage,
                        AppResources.ClearDemo, AppResources.KeepDemo);
                    if (clearDemo)
                        await _seedDataService.ClearAllDataAsync();
                }

                // Everything created before signing in is waiting to be sent.
                _cloudBackup.SyncNowAsync().FireAndForgetSafeAsync(_errorHandler);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _errorHandler.HandleError(e);
            }
            finally
            {
                IsSigningIn = false;
                _signInCts.Dispose();
                _signInCts = null;
            }
        }

        [RelayCommand]
        private void CancelSignIn() => _signInCts?.Cancel();

        [RelayCommand]
        private async Task SyncNow()
        {
            try
            {
                await _cloudBackup.SyncNowAsync();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }

        [RelayCommand]
        private async Task SignOut()
        {
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.YandexSignOutTitle, AppResources.YandexSignOutMessage,
                AppResources.YandexSignOut, AppResources.Cancel);
            if (!confirmed)
                return;

            _yandexAuth.SignOut();
            ShowUser(null);
        }

        [RelayCommand]
        private async Task SaveTags()
        {
            foreach (var tag in Tags)
            {
                await _tagRepository.SaveItemAsync(tag);
            }

            await AppShell.DisplayToastAsync(AppResources.TagsSaved);
            SemanticScreenReader.Announce(AppResources.TagsSaved);
        }

        [RelayCommand]
        private async Task DeleteTag(Tag tag)
        {
            Tags.Remove(tag);
            await _tagRepository.DeleteItemAsync(tag);
            await AppShell.DisplayToastAsync(AppResources.TagDeleted);
            SemanticScreenReader.Announce(AppResources.TagDeleted);
        }

        [RelayCommand]
        private async Task AddTag()
        {
            var tag = new Tag { Title = AppResources.NewTag, Color = SubjectPalette.DefaultColor };
            Tags.Add(tag);
            await _tagRepository.SaveItemAsync(tag);
            await AppShell.DisplayToastAsync(AppResources.TagAdded);
            SemanticScreenReader.Announce(AppResources.TagAdded);
        }

        [RelayCommand]
        private async Task Reset()
        {
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.ResetDemoTitle, AppResources.ResetDemoMessage,
                AppResources.ResetDemo, AppResources.Cancel);
            if (!confirmed)
                return;

            await _seedDataService.LoadSeedDataAsync();
            await Shell.Current.GoToAsync("//main");
        }

        [RelayCommand]
        private async Task ClearDemo()
        {
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.ClearDemoTitle, AppResources.ClearDemoMessage,
                AppResources.ClearDemo, AppResources.Cancel);
            if (!confirmed)
                return;

            await _seedDataService.ClearAllDataAsync();
            await AppShell.DisplayToastAsync(AppResources.ClearDemoDone);
            await Shell.Current.GoToAsync("//main");
        }
    }
}
