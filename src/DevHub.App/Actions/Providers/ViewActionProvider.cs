using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Services;

namespace DevHub.Actions.Providers;

/// <summary>Appearance toggles.</summary>
public sealed class ViewActionProvider(IThemeService theme) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.ToggleTheme, "Toggle Dark Theme",
            new RelayCommand(() => theme.IsDark = !theme.IsDark))
        {
            Category = "View",
            IconKind = IconKind.Palette,
            Gesture = "Ctrl+Alt+D",
        };

        yield return new AppAction(ActionIds.ToggleCompact, "Toggle Compact Mode",
            new RelayCommand(() => theme.IsCompact = !theme.IsCompact))
        {
            Category = "View",
        };
    }
}
