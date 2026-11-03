using Android.App;
using Android.Content;
using Microsoft.Maui.Handlers;

namespace StudyFlow;

public sealed class ThemedTimePickerHandler : TimePickerHandler
{
    protected override TimePickerDialog CreateTimePickerDialog(int hour, int minute)
    {
        var dark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
        var dialog = new TimePickerDialog(Context!,
            dark ? Resource.Style.StudyFlow_TimeDialog_Dark : Resource.Style.StudyFlow_TimeDialog_Light,
            (_, args) =>
            {
                VirtualView.Time = new TimeSpan(args.HourOfDay, args.Minute, 0);
                VirtualView.IsFocused = false;
            }, hour, minute, VirtualView.Format == "HH:mm" || Android.Text.Format.DateFormat.Is24HourFormat(Context));
        dialog.ShowEvent += (_, _) =>
        {
            var color = Android.Graphics.Color.ParseColor(dark ? "#DBE64C" : "#00804C");
            dialog.GetButton((int)DialogButtonType.Positive)?.SetTextColor(color);
            dialog.GetButton((int)DialogButtonType.Negative)?.SetTextColor(color);
        };
        dialog.DismissEvent += (_, _) =>
        {
            VirtualView.IsOpen = false;
            VirtualView.IsFocused = false;
        };
        return dialog;
    }
}
