namespace StudyFlow.Pages
{
    public partial class SettingsPage : ContentPage
    {
        private readonly SettingsPageModel _model;

        public SettingsPage(SettingsPageModel model)
        {
            InitializeComponent();
            BindingContext = _model = model;
            ThemeSegmentedControl.SelectedIndex = model.ThemeIndex;
        }

        private void ThemeSegmentedControl_SelectionChanged(object? sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
        {
            if (e.NewIndex is int index)
                _model.SetTheme(index);
        }
    }
}
