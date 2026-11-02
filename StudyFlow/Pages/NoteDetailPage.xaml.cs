namespace StudyFlow.Pages
{
    public partial class NoteDetailPage : ContentPage
    {
        public NoteDetailPage(NoteDetailPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}
