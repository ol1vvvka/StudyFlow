using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using StudyFlow.Models;
using System.Net;
using System.Text;

namespace StudyFlow.Services
{
    /// <summary>
    /// Mirrors materials and notes to the user's Yandex Disk app folder, one way: device → Disk.
    /// Changes wait in the database (SyncStatus columns, RemoteDeletion table) until the user is signed in and online.
    /// </summary>
    /// <remarks>
    /// Layout on Disk: Приложения/StudyFlow/&lt;subject&gt;/Материалы/&lt;file&gt; and .../Заметки/&lt;title&gt;.md.
    /// </remarks>
    public class CloudBackupService : IRecipient<NotesChangedMessage>, IRecipient<MaterialsChangedMessage>
    {
        public const string WifiOnlyKey = "cloud_wifi_only";
        public const string StorageModeKey = "cloud_storage_mode";
        private const string LastSyncKey = "cloud_last_sync";

        // Folder names are part of the data layout on Disk, so they are not translated.
        private const string NoSubjectFolder = "Без предмета";
        private const string MaterialsFolder = "Материалы";
        private const string NotesFolder = "Заметки";

        // Wait for a pause in edits so a note is not uploaded on every keystroke.
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(3);

        private readonly YandexAuthService _auth;
        private readonly YandexDiskClient _disk;
        private readonly MaterialRepository _materialRepository;
        private readonly NoteRepository _noteRepository;
        private readonly RemoteDeletionRepository _deletionRepository;
        private readonly ILogger<CloudBackupService> _logger;
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private CancellationTokenSource? _debounceCts;
        private volatile bool _rerunRequested;

        public CloudBackupService(YandexAuthService auth, YandexDiskClient disk, MaterialRepository materialRepository,
            NoteRepository noteRepository, RemoteDeletionRepository deletionRepository, ILogger<CloudBackupService> logger)
        {
            _auth = auth;
            _disk = disk;
            _materialRepository = materialRepository;
            _noteRepository = noteRepository;
            _deletionRepository = deletionRepository;
            _logger = logger;
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public bool IsRunning { get; private set; }
        public int Done { get; private set; }
        public int Total { get; private set; }
        public int ErrorCount { get; private set; }
        public int WaitingForWifiCount { get; private set; }

        public DateTime? LastSyncAt =>
            Preferences.Default.ContainsKey(LastSyncKey) ? Preferences.Default.Get(LastSyncKey, DateTime.MinValue) : null;

        public bool WifiOnly
        {
            get => Preferences.Default.Get(WifiOnlyKey, false);
            set
            {
                Preferences.Default.Set(WifiOnlyKey, value);
                RequestSync();
            }
        }

        public CloudStorageMode StorageMode
        {
            get => (CloudStorageMode)Preferences.Default.Get(StorageModeKey, (int)CloudStorageMode.DeviceAndCloud);
            set
            {
                Preferences.Default.Set(StorageModeKey, (int)value);
                RequestSync();
            }
        }

        public async Task InitializeAsync()
        {
            await _auth.InitializeAsync();
            ErrorCount = await CountErrorsAsync();
        }

        public void Receive(NotesChangedMessage message) => RequestSync();

        public void Receive(MaterialsChangedMessage message) => RequestSync();

        /// <summary>
        /// Starts sending changes after a short pause; calls within the pause start a single run.
        /// </summary>
        public void RequestSync()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            RunAfterDelayAsync(_debounceCts.Token).FireAndForgetSafeAsync();
        }

        /// <summary>
        /// Sends everything waiting right away, retrying items that failed before.
        /// </summary>
        public Task SyncNowAsync() => SyncAsync(retryErrors: true);

        private async Task RunAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(Debounce, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await SyncAsync(retryErrors: false);
        }

        private async Task SyncAsync(bool retryErrors)
        {
            if (!await _runLock.WaitAsync(0))
            {
                // A run is in progress; it starts one more when it ends, to pick up the new changes.
                _rerunRequested = true;
                return;
            }

            try
            {
                do
                {
                    _rerunRequested = false;
                    await RunOnceAsync(retryErrors);
                }
                while (_rerunRequested);
            }
            finally
            {
                _runLock.Release();
            }
        }

        private async Task RunOnceAsync(bool retryErrors)
        {
            await _auth.InitializeAsync();
            if (!_auth.IsSignedIn || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            var deletions = await _deletionRepository.ListAsync();
            var notes = await _noteRepository.ListPendingAsync(retryErrors);
            var materials = await _materialRepository.ListPendingAsync(retryErrors);
            var links = materials.Where(m => m.IsLink).ToList();
            var files = materials.Where(m => !m.IsLink).ToList();

            WaitingForWifiCount = 0;
            if (WifiOnly && !IsOnUnmeteredNetwork())
            {
                WaitingForWifiCount = files.Count;
                files.Clear();
            }

            Done = 0;
            Total = deletions.Count + notes.Count + files.Count;
            IsRunning = true;
            ReportProgress();

            var createdFolders = new HashSet<string>();
            try
            {
                foreach (var (id, path) in deletions)
                {
                    await RunItemAsync(async () =>
                    {
                        await _disk.DeleteAsync(path);
                        await _deletionRepository.DeleteItemAsync(id);
                    }, onError: null);
                }

                // Nothing to upload for links yet: they will go to Disk with the rest of the app data.
                foreach (var link in links)
                    await _materialRepository.UpdateSyncStateAsync(link, link.RemotePath, SyncStatus.Synced, string.Empty);

                foreach (var note in notes)
                {
                    await RunItemAsync(() => UploadNoteAsync(note, createdFolders),
                        error => _noteRepository.UpdateSyncStateAsync(note, note.RemotePath, SyncStatus.Error, error));
                }

                foreach (var material in files)
                {
                    await RunItemAsync(() => UploadMaterialAsync(material, createdFolders),
                        error => _materialRepository.UpdateSyncStateAsync(material, material.RemotePath, SyncStatus.Error, error));
                }

                if (StorageMode == CloudStorageMode.CloudOnly)
                    await PurgeLocalSyncedFilesAsync();

                Preferences.Default.Set(LastSyncKey, DateTime.UtcNow);
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException
                || e is YandexDiskException { StatusCode: HttpStatusCode.Unauthorized })
            {
                // Offline or signed out: stop here, everything not sent stays queued for the next run.
                _logger.LogInformation(e, "Cloud backup stopped");
            }
            finally
            {
                ErrorCount = await CountErrorsAsync();
                IsRunning = false;
                ReportProgress();
                WeakReferenceMessenger.Default.Send(new CloudSyncFinishedMessage());
            }
        }

        /// <summary>
        /// Runs one upload or deletion. A Disk error for this item is recorded and the run goes on;
        /// network errors and sign-out stop the whole run.
        /// </summary>
        private async Task RunItemAsync(Func<Task> work, Func<string, Task>? onError)
        {
            try
            {
                await work();
            }
            catch (YandexDiskException e) when (e.StatusCode != HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(e, "Cloud backup item failed");
                if (onError is not null)
                    await onError(e.Message);
            }
            catch (IOException e)
            {
                _logger.LogWarning(e, "Cloud backup could not read a local file");
                if (onError is not null)
                    await onError(e.Message);
            }

            Done++;
            ReportProgress();
        }

        private async Task UploadNoteAsync(Note note, HashSet<string> createdFolders)
        {
            var folder = $"app:/{SubjectFolder(note.SubjectName)}/{NotesFolder}";
            await EnsureFolderAsync(folder, createdFolders);
            var target = await ChooseRemotePathAsync(note.RemotePath, folder, Formatting.SafeFileName(note.DisplayTitle), ".md");

            // Moving the old copy, rather than deleting it, keeps renamed notes out of the Disk trash.
            if (note.RemotePath.Length > 0 && note.RemotePath != target)
                await _disk.MoveAsync(note.RemotePath, target);

            var markdown = $"# {note.DisplayTitle}\n\n{note.Content}\n";
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
            await _disk.UploadAsync(target, content);

            await _noteRepository.UpdateSyncStateAsync(note, target, SyncStatus.Synced, string.Empty);
        }

        private async Task UploadMaterialAsync(Material material, HashSet<string> createdFolders)
        {
            if (!File.Exists(material.FullPath))
            {
                await _materialRepository.UpdateSyncStateAsync(material, material.RemotePath, SyncStatus.Error, AppResources.CloudFileMissing);
                return;
            }

            var folder = $"app:/{SubjectFolder(material.SubjectName)}/{MaterialsFolder}";
            await EnsureFolderAsync(folder, createdFolders);
            var target = await ChooseRemotePathAsync(material.RemotePath, folder,
                Formatting.SafeFileName(material.Title), Path.GetExtension(material.FilePath));

            // A file's content never changes after import, so a rename or a new subject only moves it on Disk.
            bool alreadyThere = material.RemotePath == target
                ? await _disk.ExistsAsync(target)
                : material.RemotePath.Length > 0 && await _disk.MoveAsync(material.RemotePath, target);

            if (!alreadyThere)
            {
                await using var stream = File.OpenRead(material.FullPath);
                await _disk.UploadAsync(target, stream);
            }

            await _materialRepository.UpdateSyncStateAsync(material, target, SyncStatus.Synced, string.Empty);

            // Cloud-only: free device space after a successful mirror; metadata and RemotePath stay.
            if (StorageMode == CloudStorageMode.CloudOnly)
                DeleteLocalFile(material.FullPath);
        }

        private async Task PurgeLocalSyncedFilesAsync()
        {
            var materials = await _materialRepository.ListAsync();
            foreach (var material in materials)
            {
                if (material.IsLink || material.SyncStatus != SyncStatus.Synced || material.RemotePath.Length == 0)
                    continue;
                if (material.HasLocalFile)
                    DeleteLocalFile(material.FullPath);
            }
        }

        private static void DeleteLocalFile(string fullPath)
        {
            if (fullPath.Length == 0 || !File.Exists(fullPath))
                return;

            File.Delete(fullPath);
            var folder = Path.GetDirectoryName(fullPath);
            if (folder is not null && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }

        /// <summary>
        /// Picks "name.ext" in the folder, or "name (2).ext" and so on if another file already has that name.
        /// </summary>
        private async Task<string> ChooseRemotePathAsync(string currentPath, string folder, string name, string extension)
        {
            var candidate = $"{folder}/{name}{extension}";
            for (int i = 2; candidate != currentPath && await _disk.ExistsAsync(candidate); i++)
                candidate = $"{folder}/{name} ({i}){extension}";

            return candidate;
        }

        private async Task EnsureFolderAsync(string folder, HashSet<string> createdFolders)
        {
            // "app:/Subject/Материалы": create "app:/Subject" first, the API does not create parents.
            var parent = folder[..folder.LastIndexOf('/')];
            foreach (var path in new[] { parent, folder })
            {
                if (createdFolders.Add(path))
                    await _disk.CreateFolderAsync(path);
            }
        }

        private static string SubjectFolder(string subjectName) =>
            string.IsNullOrWhiteSpace(subjectName) ? NoSubjectFolder : Formatting.SafeFileName(subjectName);

        private static bool IsOnUnmeteredNetwork() =>
            Connectivity.Current.ConnectionProfiles.Any(p => p is ConnectionProfile.WiFi or ConnectionProfile.Ethernet);

        private async Task<int> CountErrorsAsync() =>
            await _materialRepository.CountSyncErrorsAsync() + await _noteRepository.CountSyncErrorsAsync();

        private static void ReportProgress() =>
            WeakReferenceMessenger.Default.Send(new CloudSyncStatusMessage());
    }
}
