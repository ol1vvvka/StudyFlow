using CommunityToolkit.Mvvm.Messaging;
using StudyFlow.Models;
using System.Globalization;

namespace StudyFlow.Services
{
    /// <summary>
    /// Adds, opens, renames and deletes materials, keeping the files in
    /// <see cref="Data.Constants.MaterialsDirectory"/> in sync with the Material table.
    /// </summary>
    public class MaterialService
    {
        private const int MaxPickedItems = 30;

        // Phone photos are often 4000px and several MB; 2560px keeps handwriting readable.
        private const int MaxPhotoSize = 2560;
        private const int PhotoQuality = 85;

        private readonly MaterialRepository _materialRepository;
        private readonly YandexDiskClient _yandexDisk;
        private readonly YandexAuthService _yandexAuth;

        public MaterialService(MaterialRepository materialRepository, YandexDiskClient yandexDisk, YandexAuthService yandexAuth)
        {
            _materialRepository = materialRepository;
            _yandexDisk = yandexDisk;
            _yandexAuth = yandexAuth;
        }

        /// <summary>
        /// Asks what to add (files, photos, video, a camera shot or a link) and attaches it to the subject.
        /// Pass <paramref name="subjectId"/> 0 to store the material outside any space.
        /// </summary>
        /// <returns>The number of materials added.</returns>
        public async Task<int> AddWithPromptAsync(int subjectId)
        {
            var options = new List<string>
            {
                AppResources.AddMaterialFile,
                AppResources.AddMaterialPhotos,
                AppResources.AddMaterialVideos
            };
            if (MediaPicker.Default.IsCaptureSupported)
                options.Add(AppResources.AddMaterialCamera);
            options.Add(AppResources.AddMaterialLink);

            var choice = await Shell.Current.DisplayActionSheetAsync(
                AppResources.AddMaterial, AppResources.Cancel, null, options.ToArray());

            if (choice == AppResources.AddMaterialFile)
                return await PickFilesAsync(subjectId);

            if (choice == AppResources.AddMaterialPhotos)
                return await PickPhotosAsync(subjectId);

            if (choice == AppResources.AddMaterialVideos)
                return await PickVideosAsync(subjectId);

            if (choice == AppResources.AddMaterialCamera)
                return await CapturePhotoAsync(subjectId);

            if (choice == AppResources.AddMaterialLink)
                return await AddLinkAsync(subjectId) ? 1 : 0;

            return 0;
        }

        /// <summary>
        /// Lets the user pick any files on the device and imports them.
        /// </summary>
        /// <returns>The number of files imported.</returns>
        public async Task<int> PickFilesAsync(int subjectId)
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = AppResources.AddMaterial });
            return await ImportAllAsync(subjectId, files);
        }

        /// <summary>
        /// Lets the user pick photos from the gallery and imports them.
        /// </summary>
        /// <returns>The number of photos imported.</returns>
        public async Task<int> PickPhotosAsync(int subjectId)
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(PhotoOptions(AppResources.AddMaterialPhotos));
            return await ImportAllAsync(subjectId, photos);
        }

        /// <summary>
        /// Lets the user pick videos from the gallery and imports them.
        /// </summary>
        /// <returns>The number of videos imported.</returns>
        public async Task<int> PickVideosAsync(int subjectId)
        {
            var videos = await MediaPicker.Default.PickVideosAsync(new MediaPickerOptions
            {
                Title = AppResources.AddMaterialVideos,
                SelectionLimit = MaxPickedItems
            });
            return await ImportAllAsync(subjectId, videos);
        }

        /// <summary>
        /// Takes a photo of lecture notes with the camera and imports it.
        /// </summary>
        /// <returns>1 if a photo was taken; 0 if the user cancelled.</returns>
        public async Task<int> CapturePhotoAsync(int subjectId)
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync(PhotoOptions(AppResources.AddMaterialCamera));
            if (photo is null)
                return 0;

            await ImportAsync(subjectId, photo, string.Format(AppResources.PhotoTitle, DateTime.Now));
            return 1;
        }

        /// <summary>
        /// Copies a picked file into the app's storage and records it as a material.
        /// </summary>
        /// <param name="subjectId">The subject the material belongs to.</param>
        /// <param name="file">The picked or captured file.</param>
        /// <param name="title">The title to show; the file name without extension by default.</param>
        public async Task<Material> ImportAsync(int subjectId, FileResult file, string? title = null)
        {
            var extension = Path.GetExtension(file.FileName);
            title ??= Path.GetFileNameWithoutExtension(file.FileName);

            // A folder per file lets us keep a readable name: apps that open the file show it.
            var relativePath = string.Join('/',
                subjectId.ToString(CultureInfo.InvariantCulture),
                Guid.NewGuid().ToString("N"),
                Formatting.SafeFileName(title) + extension);
            var fullPath = Path.Combine(Constants.MaterialsDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using (var source = await file.OpenReadAsync())
            await using (var target = File.Create(fullPath))
            {
                await source.CopyToAsync(target);
            }

            var material = new Material
            {
                SubjectID = subjectId,
                Title = title,
                Kind = MaterialKinds.FromExtension(extension),
                FilePath = relativePath,
                SizeBytes = new FileInfo(fullPath).Length,
                AddedAt = DateTime.UtcNow
            };

            try
            {
                await _materialRepository.SaveItemAsync(material);
            }
            catch
            {
                DeleteFile(fullPath);
                throw;
            }

            WeakReferenceMessenger.Default.Send(new MaterialsChangedMessage());
            return material;
        }

        /// <summary>
        /// Asks for a web address and a title, then saves them as a link material.
        /// </summary>
        /// <returns>True if a link was added.</returns>
        public async Task<bool> AddLinkAsync(int subjectId)
        {
            var page = Shell.Current;

            var address = await page.DisplayPromptAsync(
                AppResources.LinkPromptTitle, AppResources.LinkPromptMessage,
                accept: AppResources.Ok, cancel: AppResources.Cancel,
                placeholder: "https://", keyboard: Keyboard.Url);
            if (string.IsNullOrWhiteSpace(address))
                return false;

            if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                await page.DisplayAlertAsync(AppResources.Error, AppResources.LinkInvalid, AppResources.Ok);
                return false;
            }

            var title = await page.DisplayPromptAsync(
                AppResources.LinkPromptTitle, AppResources.LinkTitlePromptMessage,
                accept: AppResources.Save, cancel: AppResources.Cancel,
                initialValue: uri.Host);
            if (title is null)
                return false;

            await _materialRepository.SaveItemAsync(new Material
            {
                SubjectID = subjectId,
                Title = string.IsNullOrWhiteSpace(title) ? uri.Host : title.Trim(),
                Kind = MaterialKind.Link,
                Url = uri.AbsoluteUri,
                AddedAt = DateTime.UtcNow
            });

            WeakReferenceMessenger.Default.Send(new MaterialsChangedMessage());
            return true;
        }

        /// <summary>
        /// Opens a file in the app registered for its type, or a link in the browser.
        /// Cloud-only materials are downloaded from Disk into the local cache first.
        /// </summary>
        public async Task OpenAsync(Material material)
        {
            if (material.IsLink)
            {
                await Launcher.Default.OpenAsync(new Uri(material.Url));
                return;
            }

            if (!material.HasLocalFile)
                await EnsureLocalFileAsync(material);

            if (!material.HasLocalFile)
                throw new FileNotFoundException(string.Format(AppResources.MaterialFileMissing, material.Title));

            await Launcher.Default.OpenAsync(new OpenFileRequest(material.Title, new ReadOnlyFile(material.FullPath)));
        }

        private async Task EnsureLocalFileAsync(Material material)
        {
            if (material.RemotePath.Length == 0 || !_yandexAuth.IsSignedIn)
                return;

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                throw new InvalidOperationException(AppResources.MaterialCloudOffline);

            Directory.CreateDirectory(Path.GetDirectoryName(material.FullPath)!);
            await _yandexDisk.DownloadAsync(material.RemotePath, material.FullPath);
        }

        /// <summary>
        /// Shows the open / rename / delete menu for a material.
        /// </summary>
        /// <returns>True if the material was renamed or deleted, so lists should be reloaded.</returns>
        public async Task<bool> ShowActionsAsync(Material material)
        {
            var choice = await Shell.Current.DisplayActionSheetAsync(
                material.Title, AppResources.Cancel, AppResources.Delete,
                AppResources.Open, AppResources.Rename);

            if (choice == AppResources.Open)
            {
                await OpenAsync(material);
                return false;
            }

            if (choice == AppResources.Rename)
                return await RenameAsync(material);

            if (choice == AppResources.Delete)
                return await ConfirmDeleteAsync(material);

            return false;
        }

        /// <summary>
        /// Deletes the material record and its file.
        /// </summary>
        public async Task DeleteAsync(Material material)
        {
            await _materialRepository.DeleteItemAsync(material);

            if (!material.IsLink)
                DeleteFile(material.FullPath);

            WeakReferenceMessenger.Default.Send(new MaterialsChangedMessage());
        }

        /// <summary>
        /// Deletes the files of all materials of a subject.
        /// </summary>
        public void DeleteSubjectFiles(int subjectId)
        {
            var folder = Path.Combine(Constants.MaterialsDirectory, subjectId.ToString(CultureInfo.InvariantCulture));
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        /// <summary>
        /// Deletes the files of all materials.
        /// </summary>
        public void DeleteAllFiles()
        {
            if (Directory.Exists(Constants.MaterialsDirectory))
                Directory.Delete(Constants.MaterialsDirectory, recursive: true);
        }

        private async Task<bool> RenameAsync(Material material)
        {
            var title = await Shell.Current.DisplayPromptAsync(
                AppResources.Rename, AppResources.RenamePromptMessage,
                accept: AppResources.Save, cancel: AppResources.Cancel,
                initialValue: material.Title);
            if (string.IsNullOrWhiteSpace(title) || title.Trim() == material.Title)
                return false;

            material.Title = title.Trim();
            await _materialRepository.SaveItemAsync(material);
            WeakReferenceMessenger.Default.Send(new MaterialsChangedMessage());
            return true;
        }

        private async Task<bool> ConfirmDeleteAsync(Material material)
        {
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.DeleteMaterialTitle,
                string.Format(AppResources.DeleteMaterialMessage, material.Title),
                AppResources.Delete, AppResources.Cancel);
            if (!confirmed)
                return false;

            await DeleteAsync(material);
            return true;
        }

        private async Task<int> ImportAllAsync(int subjectId, IEnumerable<FileResult?>? files)
        {
            if (files is null)
                return 0;

            int count = 0;
            foreach (var file in files)
            {
                if (file is null)
                    continue;

                await ImportAsync(subjectId, file);
                count++;
            }

            return count;
        }

        private static MediaPickerOptions PhotoOptions(string title) => new()
        {
            Title = title,
            SelectionLimit = MaxPickedItems,
            MaximumWidth = MaxPhotoSize,
            MaximumHeight = MaxPhotoSize,
            CompressionQuality = PhotoQuality
        };

        private static void DeleteFile(string fullPath)
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            var folder = Path.GetDirectoryName(fullPath);
            if (folder is not null && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }

    }
}
