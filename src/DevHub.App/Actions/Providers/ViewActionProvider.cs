using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Services;
using DevHub.ViewModels;

namespace DevHub.Actions.Providers;

/// <summary>Appearance toggles and "go to page" navigation.</summary>
public sealed class ViewActionProvider(IThemeService theme, MainWindowViewModel shell) : IActionProvider
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

        // One action per sidebar page. No shortcuts: Alt+1… belong to tool windows now
        // (JetBrains convention), and the sidebar itself goes away in UI-0 step 8.
        foreach (var page in shell.Pages)
        {
            yield return new AppAction(ActionIds.NavigatePrefix + page.GetType().Name.Replace("ViewModel", ""),
                $"Go to {page.Title}",
                new RelayCommand(() => shell.SelectedPage = page))
            {
                Category = "Navigate",
                IconKind = page.Icon,
            };
        }
    }
}
