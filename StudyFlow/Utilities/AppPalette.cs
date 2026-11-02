namespace StudyFlow.Utilities;

public static class AppPalette
{
    public static Color Color(string key) => (Color)Application.Current!.Resources[key];

    public static void Apply(BindableObject target, BindableProperty property, string light, string dark) =>
        target.SetAppThemeColor(property, Color(light), Color(dark));
}
