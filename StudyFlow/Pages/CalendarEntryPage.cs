using StudyFlow.Models;

namespace StudyFlow.Pages;

public sealed class CalendarEntryPage : ContentPage
{
    private readonly CalendarRepository _repository;
    private readonly CalendarEntry _entry;
    private readonly IReadOnlyList<Subject> _subjects;
    private readonly Entry _title = new();
    private readonly Entry _location = new();
    private readonly Editor _description = new() { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 110 };
    private readonly Picker _kind = new();
    private readonly Picker _subject = new();
    private readonly Switch _allDay = new();
    private readonly CheckBox _completed = new();
    private readonly DatePicker _startDate = DateField();
    private readonly DatePicker _endDate = DateField();
    private readonly TimePicker _startTime = new() { Format = "HH:mm" };
    private readonly TimePicker _endTime = new() { Format = "HH:mm" };
    private readonly Label _error = new() { IsVisible = false };
    private readonly Button _save = new() { Text = AppResources.Save };
    private readonly Button _delete = new() { Text = AppResources.Delete };
    private bool _saving;
    public bool Changed { get; private set; }

    public CalendarEntryPage(CalendarRepository repository, CalendarEntry entry, IReadOnlyList<Subject> subjects)
    {
        _repository = repository;
        AppPalette.Apply(_error, Label.TextColorProperty, "ErrorLight", "ErrorDark");
        _entry = entry.Copy();
        _subjects = subjects;
        Title = CalendarText.Get(entry.ID == 0 ? "NewEntry" : "EditEntry");
        _title.Text = entry.Title;
        _title.MaxLength = 200;
        _location.Text = entry.Location;
        _description.Text = entry.Description;
        _kind.ItemsSource = Enum.GetNames<CalendarEntryKind>().Select(CalendarText.Get).ToList();
        _kind.SelectedIndex = (int)entry.Kind;
        _subject.Items.Add(CalendarText.Get("NoSubject"));
        foreach (var subject in subjects) _subject.Items.Add(subject.Name);
        _subject.SelectedIndex = Math.Max(0, subjects.ToList().FindIndex(s => s.ID == entry.SubjectID) + 1);
        _startDate.Date = entry.Start.Date;
        _endDate.Date = entry.IsAllDay ? entry.End.AddDays(-1).Date : entry.End.Date;
        _startTime.Time = entry.Start.TimeOfDay;
        _endTime.Time = entry.End.TimeOfDay;
        _allDay.IsToggled = entry.IsAllDay;
        _completed.IsChecked = entry.IsCompleted;

        var body = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(20), MaximumWidthRequest = 760, HorizontalOptions = LayoutOptions.Fill };
        body.Add(Field("Title", _title));
        body.Add(Field("Kind", _kind));
        body.Add(ToggleRow(CalendarText.Get("AllDay"), _allDay));
        body.Add(Field("Start", DateTimeRow(_startDate, _startTime)));
        body.Add(Field("End", DateTimeRow(_endDate, _endTime)));
        body.Add(Field("Subject", _subject));
        body.Add(Field("Location", _location));
        body.Add(Field("Description", _description));
        var completionRow = ToggleRow(CalendarText.Get("Completed"), _completed);
        body.Add(completionRow);
        body.Add(_error);
        body.Add(_save);
        var cancel = new Button { Text = AppResources.Cancel };
        cancel.SetDynamicResource(StyleProperty, "SecondaryButton");
        cancel.Clicked += async (_, _) => { if (!_saving) await Navigation.PopModalAsync(); };
        body.Add(cancel);
        if (entry.ID != 0)
        {
            _delete.SetDynamicResource(StyleProperty, "SecondaryButton");
            _delete.Clicked += async (_, _) => await DeleteAsync();
            body.Add(_delete);
        }
        _allDay.Toggled += (_, _) => UpdateTimes();
        _kind.SelectedIndexChanged += (_, _) => completionRow.IsVisible = _kind.SelectedIndex == (int)CalendarEntryKind.Task;
        completionRow.IsVisible = entry.Kind == CalendarEntryKind.Task;
        UpdateTimes();
        _save.Clicked += async (_, _) => await SaveAsync();
        Content = new ScrollView { Content = body };
    }

    private void UpdateTimes() => _startTime.IsVisible = _endTime.IsVisible = !_allDay.IsToggled;

    private async Task SaveAsync()
    {
        if (_saving) return;
        try
        {
            SetBusy(true);
            _entry.Title = _title.Text ?? "";
            _entry.Description = _description.Text ?? "";
            _entry.Location = _location.Text ?? "";
            _entry.Kind = (CalendarEntryKind)_kind.SelectedIndex;
            _entry.SubjectID = _subject.SelectedIndex > 0 ? _subjects[_subject.SelectedIndex - 1].ID : null;
            _entry.IsAllDay = _allDay.IsToggled;
            _entry.Start = (_startDate.Date ?? DateTime.Today).Date + (_entry.IsAllDay ? TimeSpan.Zero : _startTime.Time ?? TimeSpan.Zero);
            _entry.End = (_endDate.Date ?? DateTime.Today).Date + (_entry.IsAllDay ? TimeSpan.Zero : _endTime.Time ?? TimeSpan.Zero);
            if (_entry.IsAllDay) _entry.End = _entry.End.AddDays(1);
            _entry.IsCompleted = _entry.Kind == CalendarEntryKind.Task && _completed.IsChecked;
            await _repository.SaveAsync(_entry);
            Changed = true;
            await Navigation.PopModalAsync();
        }
        catch (Exception ex) { _error.Text = ex.Message; _error.IsVisible = true; }
        finally { SetBusy(false); }
    }

    private async Task DeleteAsync()
    {
        if (_saving || !await DisplayAlertAsync(AppResources.Delete, string.Format(CalendarText.Get("DeleteConfirm"), _entry.Title), AppResources.Delete, AppResources.Cancel)) return;
        try
        {
            SetBusy(true);
            await _repository.DeleteAsync(_entry.ID);
            Changed = true;
            await Navigation.PopModalAsync();
        }
        catch (Exception ex) { _error.Text = ex.Message; _error.IsVisible = true; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy) { _saving = busy; _save.IsEnabled = _delete.IsEnabled = !busy; }
    protected override bool OnBackButtonPressed() => _saving || base.OnBackButtonPressed();
    private static DatePicker DateField() => new() { MinimumDate = new DateTime(1900, 1, 1), MaximumDate = new DateTime(2100, 12, 31), Format = "dd.MM.yyyy" };
    private static View Field(string key, View control) => new VerticalStackLayout { Spacing = 2, Children = { new Label { Text = CalendarText.Get(key), FontFamily = "NunitoSemiBold", FontSize = 14 }, control } };
    private static Grid DateTimeRow(View date, View time)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 12 };
        grid.Add(date); grid.Add(time, 1); return grid;
    }
    private static Grid ToggleRow(string text, View toggle)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        grid.Add(new Label { Text = text, VerticalOptions = LayoutOptions.Center }); grid.Add(toggle, 1); return grid;
    }
}
