namespace StudyFlow.Models
{
    /// <summary>
    /// Colors and icons a subject can be decorated with.
    /// </summary>
    public static class SubjectPalette
    {
        public const string DefaultColor = "#C7FF2E";

        public static IReadOnlyList<ColorData> ColorOptions { get; } =
        [
            new(DefaultColor, AppResources.ColorGreen),
            new("#3068DF", AppResources.ColorBlue),
            new("#512BD4", AppResources.ColorViolet),
            new("#2E9E44", AppResources.ColorGreen),
            new("#D4A017", AppResources.ColorYellow),
            new("#F06A20", AppResources.ColorOrange),
            new("#E0245E", AppResources.ColorRed),
            new("#607D8B", AppResources.ColorGray)
        ];

        public static IReadOnlyList<IconData> IconOptions { get; } =
        [
            new() { Icon = FluentUI.book_24_regular, Description = AppResources.IconBook },
            new() { Icon = FluentUI.math_formula_24_regular, Description = AppResources.IconMath },
            new() { Icon = FluentUI.calculator_24_regular, Description = AppResources.IconCalculator },
            new() { Icon = FluentUI.beaker_24_regular, Description = AppResources.IconChemistry },
            new() { Icon = FluentUI.leaf_one_24_regular, Description = AppResources.IconBiology },
            new() { Icon = FluentUI.code_24_regular, Description = AppResources.IconCode },
            new() { Icon = FluentUI.translate_24_regular, Description = AppResources.IconLanguages },
            new() { Icon = FluentUI.history_24_regular, Description = AppResources.IconHistory },
            new() { Icon = FluentUI.earth_24_regular, Description = AppResources.IconGeography },
            new() { Icon = FluentUI.scales_24_regular, Description = AppResources.IconLaw },
            new() { Icon = FluentUI.brain_circuit_24_regular, Description = AppResources.IconPsychology },
            new() { Icon = FluentUI.music_note_2_24_regular, Description = AppResources.IconMusic },
            new() { Icon = FluentUI.paint_brush_24_regular, Description = AppResources.IconArt },
            new() { Icon = FluentUI.dumbbell_24_regular, Description = AppResources.IconSport }
        ];
    }
}
