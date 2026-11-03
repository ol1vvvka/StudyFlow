using Android.App;
using Android.Content.PM;
using Android.OS;

namespace StudyFlow
{
    [Activity(Theme = "@style/StudyFlow.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            var theme = (AppTheme)Preferences.Default.Get(App.ThemePreferenceKey, (int)AppTheme.Unspecified);
            SetTheme(SplashTheme(theme));
            base.OnCreate(savedInstanceState);
            UpdateSplashTheme(theme);
        }

        public void UpdateSplashTheme(AppTheme theme)
        {
            // Android сохраняет тему системной заставки для следующего холодного запуска.
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
                SplashScreen?.SetSplashScreenTheme(SplashTheme(theme));
        }

        private static int SplashTheme(AppTheme theme) => theme switch
        {
            AppTheme.Light => Resource.Style.StudyFlow_SplashTheme_Light,
            AppTheme.Dark => Resource.Style.StudyFlow_SplashTheme_Dark,
            _ => Resource.Style.StudyFlow_SplashTheme
        };
    }
}
