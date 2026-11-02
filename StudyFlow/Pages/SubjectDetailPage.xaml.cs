namespace StudyFlow.Pages
{
    public partial class SubjectDetailPage : ContentPage
    {
        public SubjectDetailPage(SubjectDetailPageModel model)
        {
            InitializeComponent();

            BindingContext = model;
        }
    }
}
