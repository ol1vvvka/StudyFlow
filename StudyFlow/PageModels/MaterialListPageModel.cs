using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    public partial class MaterialListPageModel : ObservableObject, IMaterialPageModel, IRecipient<CloudSyncFinishedMessage>
    {
        private readonly MaterialRepository _materialRepository;
        private readonly MaterialService _materialService;
        private readonly ModalErrorHandler _errorHandler;
        private List<Material> _allMaterials = [];

        [ObservableProperty]
        private List<Material> _materials = [];

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _emptyText = AppResources.MaterialsAllEmpty;

        public MaterialListPageModel(MaterialRepository materialRepository, MaterialService materialService, ModalErrorHandler errorHandler)
        {
            _materialRepository = materialRepository;
            _materialService = materialService;
            _errorHandler = errorHandler;
            WeakReferenceMessenger.Default.Register(this);
        }

        // Refreshes the cloud icons once a backup run has finished.
        public void Receive(CloudSyncFinishedMessage message) =>
            LoadData().FireAndForgetSafeAsync(_errorHandler);

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var query = SearchText.Trim();

            Materials = query.Length == 0
                ? _allMaterials
                : _allMaterials
                    .Where(m => m.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                        || m.SubjectName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

            EmptyText = _allMaterials.Count == 0 ? AppResources.MaterialsAllEmpty : AppResources.MaterialsNotFound;
        }

        private async Task LoadData()
        {
            _allMaterials = await _materialRepository.ListAsync();
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
        private async Task AddMaterial()
        {
            try
            {
                // subjectId 0 — material outside any space.
                int count = await _materialService.AddWithPromptAsync(0);
                if (count > 0)
                {
                    await LoadData();
                    await AppShell.DisplayToastAsync(string.Format(AppResources.MaterialsAdded, count));
                }
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
                    await LoadData();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
        }
    }
}
