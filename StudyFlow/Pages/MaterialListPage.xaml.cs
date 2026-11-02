namespace StudyFlow.Pages
{
    public partial class MaterialListPage : ContentPage
    {
        public MaterialListPage(MaterialListPageModel model)
        {
            BindingContext = model;
            InitializeComponent();
        }
    }
}
