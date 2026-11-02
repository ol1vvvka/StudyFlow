using System.Resources;

namespace StudyFlow.Resources.Strings;

public static class CalendarText
{
    private static readonly ResourceManager Resources = new("StudyFlow.Resources.Strings.CalendarResources", typeof(CalendarText).Assembly);
    public static string Get(string key) => Resources.GetString(key, AppResources.Culture) ?? key;
    public static string Title => Get("Calendar");
}
