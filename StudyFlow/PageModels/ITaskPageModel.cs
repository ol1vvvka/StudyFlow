using CommunityToolkit.Mvvm.Input;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    public interface ITaskPageModel
    {
        IAsyncRelayCommand<StudyTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}
