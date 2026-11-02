using StudyFlow.Models;
using System.Globalization;

namespace StudyFlow.Pages;

public sealed class CalendarPage : ContentPage
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");
    private readonly CalendarRepository _repository;
    private readonly SubjectRepository _subjects;
    private readonly Label _period = new() { FontFamily = "NunitoBold", FontSize = 20, VerticalOptions = LayoutOptions.Center };
    private readonly Picker _modePicker = new() { WidthRequest = 120, Title = CalendarText.Title };
    private readonly VerticalStackLayout _body = new() { Spacing = 14 };
    private readonly ScrollView _scroll;
    private readonly Label _error = new() { IsVisible = false };
    private readonly ActivityIndicator _busy = new();
    private readonly DatePicker _jumpDate = new() { Date = DateTime.Today, Format = "dd.MM.yyyy", MinimumDate = new DateTime(1901, 1, 1), MaximumDate = new DateTime(2099, 12, 31), WidthRequest = 145 };
    private DateTime _date = DateTime.Today;
    private CalendarViewMode _mode = CalendarViewMode.Month;
    private List<CalendarEntry> _entries = [];
    private int _loadVersion;
    private bool _opening;
    private bool _settingDate;
    private double _renderWidth;

    public CalendarPage(CalendarRepository repository, SubjectRepository subjects)
    {
        _repository = repository;
        _subjects = subjects;
        Title = CalendarText.Title;
        AppPalette.Apply(_error, Label.TextColorProperty, "ErrorLight", "ErrorDark");
        _modePicker.ItemsSource = Enum.GetNames<CalendarViewMode>().Select(CalendarText.Get).ToList();
        _modePicker.SelectedIndex = (int)_mode;
        _modePicker.SelectedIndexChanged += async (_, _) =>
        {
            if (_modePicker.SelectedIndex < 0) return;
            _mode = (CalendarViewMode)_modePicker.SelectedIndex;
            await ReloadAsync();
        };
        var add = new ToolbarItem { Text = CalendarText.Get("Add"), IconImageSource = (ImageSource)Application.Current!.Resources["IconAdd"] };
        add.Clicked += async (_, _) => await EditAsync(null, DefaultStart());
        ToolbarItems.Add(add);
        var root = new Grid
        {
            Padding = new Thickness(12), RowSpacing = 8,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };
        var controls = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        controls.Add(_period); controls.Add(_modePicker, 1);
        root.Add(controls);
        var navigation = new HorizontalStackLayout { Spacing = 8 };
        navigation.Add(ActionButton("‹", async () => await MoveAsync(-1), CalendarText.Get("Previous")));
        navigation.Add(ActionButton(CalendarText.Get("Today"), async () => { _date = DateTime.Today; await ReloadAsync(); }));
        navigation.Add(ActionButton("›", async () => await MoveAsync(1), CalendarText.Get("Next")));
        _jumpDate.DateSelected += async (_, e) => { if (!_settingDate && e.NewDate is DateTime date) { _date = date; await ReloadAsync(); } };
        navigation.Add(_jumpDate);
        root.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = navigation }, 0, 1);
        var status = new VerticalStackLayout { Children = { new Label { Text = CalendarText.Get("LocalTime"), FontSize = 12 }, _error, _busy } };
        root.Add(status, 0, 2);
        _scroll = new ScrollView { Content = _body };
        root.Add(_scroll, 0, 3);
        Content = root;
        SizeChanged += (_, _) =>
        {
            if (_mode == CalendarViewMode.Day && _body.Count > 0 && Width > 0 && Math.Abs(Width - _renderWidth) > 1) Render();
        };
    }

    protected override async void OnAppearing() { base.OnAppearing(); await ReloadAsync(); }

    private async Task ReloadAsync()
    {
        int version = ++_loadVersion;
        _busy.IsRunning = true;
        _busy.IsVisible = true;
        _error.IsVisible = false;
        try
        {
            var range = CalendarDates.Range(_date, _mode);
            _settingDate = true;
            _jumpDate.Date = _date;
            _settingDate = false;
            _period.Text = _mode switch
            {
                CalendarViewMode.Year => _date.Year.ToString(Culture),
                CalendarViewMode.Month => _date.ToString("MMMM yyyy", Culture),
                CalendarViewMode.Week => $"{range.Start:dd.MM} – {range.End.AddDays(-1):dd.MM.yyyy}",
                _ => _date.ToString("d MMMM, dddd", Culture)
            };
            if (_mode == CalendarViewMode.Month)
                range = (CalendarDates.Monday(range.Start), CalendarDates.Monday(range.Start).AddDays(42));
            var entries = await _repository.ListAsync(range.Start, range.End);
            if (version != _loadVersion) return;
            _entries = entries;
            Render();
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) { _body.Clear(); _error.Text = ex.Message; _error.IsVisible = true; _body.Add(ActionButton(CalendarText.Get("Reload"), ReloadAsync)); }
        }
        finally { if (version == _loadVersion) { _busy.IsRunning = false; _busy.IsVisible = false; } }
    }

    private void Render()
    {
        _renderWidth = Width;
        _body.Clear();
        if (_mode == CalendarViewMode.Year) RenderYear();
        else if (_mode == CalendarViewMode.Month) RenderMonth();
        else RenderTimeline(_mode == CalendarViewMode.Week ? CalendarDates.Monday(_date) : _date.Date, _mode == CalendarViewMode.Week ? 7 : 1);
    }

    private void RenderYear()
    {
        var months = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.SpaceAround };
        for (int month = 1; month <= 12; month++)
        {
            var first = new DateTime(_date.Year, month, 1);
            var stack = new VerticalStackLayout { WidthRequest = 270, Spacing = 4, Padding = 8, Margin = new Thickness(2) };
            stack.Add(ActionButton(first.ToString("MMMM", Culture), () => OpenMonthAsync(first)));
            var grid = SevenColumns();
            AddWeekdays(grid, 11);
            int offset = ((int)first.DayOfWeek + 6) % 7;
            for (int day = 1; day <= DateTime.DaysInMonth(first.Year, month); day++)
            {
                var date = first.AddDays(day - 1);
                int index = offset + day - 1;
                bool hasEntries = _entries.Any(e => CalendarDates.Overlaps(e, date, date.AddDays(1)));
                var button = ActionButton(day.ToString(Culture) + (hasEntries ? " ·" : ""), () => OpenDayAsync(date));
                button.FontSize = 11; button.Padding = 0; button.MinimumWidthRequest = 0; button.HeightRequest = 34; button.MinimumHeightRequest = 34;
                if (date == DateTime.Today) Highlight(button);
                grid.Add(button, index % 7, index / 7 + 1);
            }
            stack.Add(grid);
            stack.Add(new Label { Text = string.Format(CalendarText.Get("Count"), _entries.Count(e => CalendarDates.Overlaps(e, first, first.AddMonths(1)))), FontSize = 12 });
            months.Add(stack);
        }
        _body.Add(months);
    }

    private void RenderMonth()
    {
        var first = new DateTime(_date.Year, _date.Month, 1);
        var start = CalendarDates.Monday(first);
        var grid = SevenColumns();
        grid.RowSpacing = 3;
        grid.ColumnSpacing = 3;
        AddWeekdays(grid, 13);
        for (int index = 0; index < 42; index++)
        {
            var date = start.AddDays(index);
            int count = _entries.Count(e => CalendarDates.Overlaps(e, date, date.AddDays(1)));
            var button = ActionButton(date.Day.ToString(Culture) + (count > 0 ? $"\n· {count}" : ""), async () => { _date = date; await ReloadAsync(); });
            button.FontSize = 14; button.Padding = 0; button.MinimumWidthRequest = 0; button.HeightRequest = 64;
            button.Opacity = date.Month == first.Month ? 1 : 0.5;
            SemanticProperties.SetDescription(button, $"{date:dd.MM.yyyy}. {string.Format(CalendarText.Get("Count"), count)}");
            if (date == _date.Date) Highlight(button);
            else if (date == DateTime.Today) { button.BorderWidth = 1; AppPalette.Apply(button, Button.BorderColorProperty, "Primary", "PrimaryDark"); }
            grid.Add(button, index % 7, index / 7 + 1);
        }
        _body.Add(grid);
        _body.Add(ActionButton($"{_date:d MMMM} · {CalendarText.Get("OpenDay")}", () => OpenDayAsync(_date)));
        RenderAgenda(_date);
    }

    private void RenderAgenda(DateTime date)
    {
        var entries = _entries.Where(e => CalendarDates.Overlaps(e, date.Date, date.Date.AddDays(1))).ToList();
        if (entries.Count == 0) _body.Add(new Label { Text = CalendarText.Get("Empty"), FontSize = 14, Margin = new Thickness(4, 8) });
        foreach (var entry in entries) _body.Add(EntryButton(entry));
        _body.Add(ActionButton(CalendarText.Get("Add"), () => EditAsync(null, DefaultStart())));
    }

    private void RenderTimeline(DateTime start, int days)
    {
        double dayWidth = days == 1 ? Math.Max(260, Width - 96) : 180;
        const double hourHeight = 90;
        var table = new Grid { WidthRequest = 56 + days * dayWidth, ColumnSpacing = 0, RowSpacing = 0 };
        table.ColumnDefinitions.Add(new ColumnDefinition(56));
        for (int day = 0; day < days; day++) table.ColumnDefinitions.Add(new ColumnDefinition(dayWidth));
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        table.RowDefinitions.Add(new RowDefinition(24 * hourHeight));
        table.Add(new Label { Text = CalendarText.Get("AllDay"), FontSize = 11 }, 0, 1);
        for (int day = 0; day < days; day++)
        {
            var date = start.AddDays(day);
            var header = ActionButton(date.ToString("ddd, dd.MM", Culture), () => OpenDayAsync(date));
            header.FontSize = 14;
            if (date == DateTime.Today) Highlight(header);
            table.Add(header, day + 1);
            var allDay = new VerticalStackLayout { Spacing = 2, Padding = 2 };
            foreach (var entry in _entries.Where(e => e.IsAllDay && CalendarDates.Overlaps(e, date, date.AddDays(1))))
                allDay.Add(EntryButton(entry, compact: true));
            table.Add(allDay, day + 1, 1);
            var canvas = new AbsoluteLayout { HeightRequest = 24 * hourHeight, WidthRequest = dayWidth };
            for (int hour = 0; hour < 24; hour++)
            {
                var at = date.AddHours(hour);
                var slot = new Border { StrokeThickness = 0.5, BackgroundColor = Colors.Transparent, Padding = 0 };
                slot.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(AppPalette.Color("BorderLight")), new SolidColorBrush(AppPalette.Color("BorderDark")));
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await EditAsync(null, at);
                slot.GestureRecognizers.Add(tap);
                SemanticProperties.SetDescription(slot, string.Format(CalendarText.Get("NewAt"), at));
                AbsoluteLayout.SetLayoutBounds(slot, new Rect(0, hour * hourHeight, dayWidth, hourHeight));
                canvas.Add(slot);
            }
            foreach (var placement in CalendarLayout.Arrange(_entries, date))
            {
                double width = (dayWidth - 4) / placement.Columns;
                var button = EntryButton(placement.Entry, compact: true);
                button.Padding = 3; button.FontSize = 11; button.MinimumWidthRequest = 0; button.MinimumHeightRequest = 0;
                double height = Math.Max(28, (placement.EndMinute - placement.StartMinute) * hourHeight / 60 - 2);
                double top = Math.Min(placement.StartMinute * hourHeight / 60, 24 * hourHeight - height);
                AbsoluteLayout.SetLayoutBounds(button, new Rect(2 + placement.Column * width, top, width - 2, height));
                canvas.Add(button);
            }
            table.Add(canvas, day + 1, 2);
        }
        var hours = new AbsoluteLayout { HeightRequest = 24 * hourHeight };
        for (int hour = 0; hour < 24; hour++)
        {
            var label = new Label { Text = $"{hour:00}:00", FontSize = 12 };
            AbsoluteLayout.SetLayoutBounds(label, new Rect(0, hour * hourHeight, 54, 24));
            hours.Add(label);
        }
        table.Add(hours, 0, 2);
        _body.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = table });
    }

    private Button EntryButton(CalendarEntry entry, bool compact = false)
    {
        string time = entry.IsAllDay ? CalendarText.Get("AllDay") : entry.Start.Date == entry.End.Date ? $"{entry.Start:HH:mm}–{entry.End:HH:mm}" : $"{entry.Start:dd.MM HH:mm}–{entry.End:dd.MM HH:mm}";
        string kind = CalendarText.Get(entry.Kind.ToString());
        var button = ActionButton($"{(entry.IsCompleted ? "✓ " : "")}{time} · {kind}\n{entry.Title}" + (!compact && entry.Location.Length > 0 ? $"\n{entry.Location}" : ""), () => EditAsync(entry, entry.Start));
        button.LineBreakMode = LineBreakMode.TailTruncation;
        button.HorizontalOptions = LayoutOptions.Fill;
        button.BorderWidth = 2;
        AppPalette.Apply(button, Button.BorderColorProperty, "BorderLight", "BorderDark");
        if (entry.IsCompleted) button.Opacity = 0.65;
        SemanticProperties.SetDescription(button, $"{entry.Title}. {kind}. {entry.Start:dd.MM HH:mm} – {entry.End:dd.MM HH:mm}");
        return button;
    }

    private async Task EditAsync(CalendarEntry? entry, DateTime start)
    {
        if (_opening) return;
        _opening = true;
        try
        {
            var subjects = await _subjects.ListAsync();
            var editor = new CalendarEntryPage(_repository, entry ?? new CalendarEntry { Start = start, End = start.AddHours(1) }, subjects);
            await Navigation.PushModalAsync(new NavigationPage(editor));
        }
        catch (Exception ex) { await DisplayAlertAsync(AppResources.Error, ex.Message, AppResources.Ok); }
        finally { _opening = false; }
    }

    private DateTime DefaultStart() => _date.Date.AddHours(_date.Date == DateTime.Today ? Math.Min(22, DateTime.Now.Hour + 1) : 9);
    private async Task OpenDayAsync(DateTime date) { _date = date; await SetModeAsync(CalendarViewMode.Day); }
    private async Task OpenMonthAsync(DateTime date) { _date = date; await SetModeAsync(CalendarViewMode.Month); }
    private Task SetModeAsync(CalendarViewMode mode)
    {
        if (_modePicker.SelectedIndex == (int)mode) return ReloadAsync();
        _modePicker.SelectedIndex = (int)mode;
        return Task.CompletedTask;
    }
    private async Task MoveAsync(int delta)
    {
        var date = _mode switch
        {
            CalendarViewMode.Year => _date.AddYears(delta), CalendarViewMode.Month => _date.AddMonths(delta),
            CalendarViewMode.Week => _date.AddDays(7 * delta), _ => _date.AddDays(delta)
        };
        if (date.Year is < 1901 or > 2099) return;
        _date = date;
        await ReloadAsync();
    }

    private static Grid SevenColumns()
    {
        var grid = new Grid();
        for (int i = 0; i < 7; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < 7; i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        return grid;
    }
    private static void AddWeekdays(Grid grid, int fontSize)
    {
        var monday = new DateTime(2026, 11, 2);
        for (int i = 0; i < 7; i++) grid.Add(new Label { Text = monday.AddDays(i).ToString("ddd", Culture), FontSize = fontSize, HorizontalTextAlignment = TextAlignment.Center }, i);
    }
    private static Button ActionButton(string text, Func<Task> action, string? accessibility = null)
    {
        var button = new Button { Text = text, FontFamily = "NunitoSemiBold", FontSize = 14, Padding = new Thickness(10, 6), MinimumHeightRequest = 44, CornerRadius = 10 };
        AppPalette.Apply(button, Button.BackgroundColorProperty, "LightSecondaryBackground", "DarkSecondaryBackground");
        AppPalette.Apply(button, Button.TextColorProperty, "DarkOnLightBackground", "LightOnDarkBackground");
        if (accessibility != null) SemanticProperties.SetDescription(button, accessibility);
        button.Clicked += async (_, _) => await action();
        return button;
    }
    private static void Highlight(Button button)
    {
        AppPalette.Apply(button, Button.BackgroundColorProperty, "ActionBackground", "ActionBackground");
        AppPalette.Apply(button, Button.TextColorProperty, "ActionText", "ActionText");
    }
}
