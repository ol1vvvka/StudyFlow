global using StudyFlow.Resources.Strings;
global using StudyFlow.Utilities;

namespace StudyFlow.Data
{
    public static class Constants { public static string DatabasePath => throw new InvalidOperationException("Tests must use an isolated database."); }
}

namespace StudyFlow.Resources.Strings
{
    public static class CalendarText { public static string Get(string key) => key; }
}
