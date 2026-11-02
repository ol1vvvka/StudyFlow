namespace StudyFlow.Pages
{
    public partial class SubjectListPage : ContentPage
    {
        public SubjectListPage(SubjectListPageModel model)
        {
            BindingContext = model;
            InitializeComponent();
        }
    }
}
