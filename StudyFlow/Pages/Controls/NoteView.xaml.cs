using StudyFlow.Models;

namespace StudyFlow.Pages.Controls
{
    public partial class NoteView
    {
        public NoteView()
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
            typeof(NoteView),
            true,
            propertyChanged: (bindable, _, _) => ((NoteView)bindable).UpdateSubjectVisibility());

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
            SubjectRow.IsVisible = ShowSubject && BindingContext is Note { HasSubject: true };
    }
}
