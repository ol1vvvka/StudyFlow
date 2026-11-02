namespace StudyFlow.Pages
{
    public partial class NoteListPage : ContentPage
    {
        public NoteListPage(NoteListPageModel model)
        {
            BindingContext = model;
            InitializeComponent();
        }
    }
}
