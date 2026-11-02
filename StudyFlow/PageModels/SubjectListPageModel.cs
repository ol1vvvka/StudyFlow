using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyFlow.Models;

namespace StudyFlow.PageModels
{
    public partial class SubjectListPageModel : ObservableObject
    {
        private readonly SubjectRepository _subjectRepository;

        [ObservableProperty]
        private List<Subject> _subjects = [];

        [ObservableProperty]
        private Subject? selectedSubject;

        public SubjectListPageModel(SubjectRepository subjectRepository)
        {
            _subjectRepository = subjectRepository;
        }

        [RelayCommand]
        private async Task Appearing()
        {
            Subjects = await _subjectRepository.ListAsync();
        }

        [RelayCommand]
        Task? NavigateToSubject(Subject subject)
            => subject is null ? Task.CompletedTask : Shell.Current.GoToAsync($"subject?id={subject.ID}");

        [RelayCommand]
        async Task AddSubject()
        {
            await Shell.Current.GoToAsync($"subject");
        }
    }
}
