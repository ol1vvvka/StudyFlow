using CommunityToolkit.Mvvm.Input;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    /// <summary>
    /// A page that shows <see cref="Pages.Controls.NoteView"/> items.
    /// </summary>
    public interface INotePageModel
    {
        IAsyncRelayCommand<Note> OpenNoteCommand { get; }
    }
}
