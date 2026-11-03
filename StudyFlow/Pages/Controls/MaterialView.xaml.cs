using StudyFlow.Models;

namespace StudyFlow.Pages.Controls
{
    public partial class MaterialView
    {
        public MaterialView()
        {
            InitializeComponent();
            UpdateSubjectVisibility();
        }

        /// <summary>
        /// Whether to show the subject name above the title. Off on the subject's own page.
        /// </summary>
        public static readonly BindableProperty ShowSubjectProperty = BindableProperty.Create(
            nameof(ShowSubject),
            typeof(bool),
            typeof(MaterialView),
            true,
            propertyChanged: (bindable, _, _) => ((MaterialView)bindable).UpdateSubjectVisibility());

        public bool ShowSubject
        {
            get => (bool)GetValue(ShowSubjectProperty);
            set => SetValue(ShowSubjectProperty, value);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            UpdateSubjectVisibility();
        }

        private void UpdateSubjectVisibility() =>
            SubjectLabel.IsVisible = ShowSubject && BindingContext is Material { HasSubject: true };
    }
}
