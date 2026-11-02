using Android.App;
using Android.Content;
using Android.Content.PM;
using StudyFlow.Services.Yandex;

namespace StudyFlow
{
    /// <summary>
    /// Receives the redirect from Yandex ID after sign-in (YandexConfig.RedirectUri) and hands it to WebAuthenticator.
    /// </summary>
    [Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = YandexConfig.CallbackScheme)]
    public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
    {
    }
}
