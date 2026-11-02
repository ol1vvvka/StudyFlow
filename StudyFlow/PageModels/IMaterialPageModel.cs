using CommunityToolkit.Mvvm.Input;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    /// <summary>
    /// A page that shows <see cref="Pages.Controls.MaterialView"/> items.
    /// </summary>
    public interface IMaterialPageModel
    {
        IAsyncRelayCommand<Material> OpenMaterialCommand { get; }
        IAsyncRelayCommand<Material> MaterialActionsCommand { get; }
    }
}
