namespace StudyFlow.Pages;

public partial class LaunchPage : ContentPage
{
    private bool _started;

    public LaunchPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        try
        {
            if (AnimationsEnabled())
                await Task.WhenAll(Orb.ScaleToAsync(1.12, 2000, Easing.SinInOut), Orb.TranslateToAsync(16, -20, 2000, Easing.SinInOut), Halo.FadeToAsync(0.7, 2000), Wordmark.FadeToAsync(1, 900));
            else Wordmark.Opacity = 1;
        }
        finally
        {
            if (Window is { } window) window.Page = new AppShell();
        }
    }

    private static bool AnimationsEnabled()
    {
#if ANDROID
        return Android.Provider.Settings.Global.GetFloat(Android.App.Application.Context.ContentResolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1) > 0;
#elif WINDOWS
        return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#else
        return !OperatingSystem.IsIOS() || !UIKit.UIAccessibility.IsReduceMotionEnabled;
#endif
    }
}
