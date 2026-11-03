using System.Resources;

namespace StudyFlow.Resources.Strings;

public static class DesignText
{
    private static readonly ResourceManager Resources = new("StudyFlow.Resources.Strings.DesignResources", typeof(DesignText).Assembly);
    private static string Get(string key) => Resources.GetString(key, AppResources.Culture) ?? key;
    public static string Headline => Get(nameof(Headline));
    public static string Subtitle => Get(nameof(Subtitle));
    public static string Calendar => Get(nameof(Calendar));
    public static string SplashSubtitle => Get(nameof(SplashSubtitle));
}
