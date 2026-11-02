namespace StudyFlow.Pages.Controls
{
    public partial class MaterialView
    {
        public MaterialView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Whether to show the subject name above the title. Off on the subject's own page.
        /// </summary>
        public static readonly BindableProperty ShowSubjectProperty = BindableProperty.Create(
            nameof(ShowSubject),
            typeof(bool),
            typeof(MaterialView),
            true);

        public bool ShowSubject
        {
            get => (bool)GetValue(ShowSubjectProperty);
            set => SetValue(ShowSubjectProperty, value);
        }
    }
}
