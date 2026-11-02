using Microsoft.Extensions.DependencyInjection;

namespace StudyFlow
{
    public partial class App : Application
    {
        public const string ThemePreferenceKey = "app_theme";

        public App()
        {
            InitializeComponent();
            UserAppTheme = (AppTheme)Preferences.Default.Get(ThemePreferenceKey, (int)AppTheme.Unspecified);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
