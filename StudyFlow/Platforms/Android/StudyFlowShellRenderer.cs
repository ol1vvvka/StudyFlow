using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace StudyFlow;

public sealed class StudyFlowShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        => new TabAppearance(this, shellItem);

    private sealed class TabAppearance(IShellContext context, ShellItem item)
        : ShellBottomNavViewAppearanceTracker(context, item)
    {
        public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
        {
            base.SetAppearance(bottomView, appearance);
            bottomView.ItemHorizontalTranslationEnabled = false;
            bottomView.ItemTextAppearanceActive = Resource.Style.StudyFlow_TabLabel;
            bottomView.ItemTextAppearanceInactive = Resource.Style.StudyFlow_TabLabel;
            bottomView.Post(() => FitLabels(bottomView));
        }

        private static void FitLabels(Android.Views.View view)
        {
            if (view is Android.Widget.TextView label)
                AndroidX.Core.Widget.TextViewCompat.SetAutoSizeTextTypeUniformWithConfiguration(
                    label, 9, 11, 1, (int)Android.Util.ComplexUnitType.Sp);
            if (view is Android.Views.ViewGroup group)
                for (var i = 0; i < group.ChildCount; i++)
                    if (group.GetChildAt(i) is { } child) FitLabels(child);
        }
    }
}
