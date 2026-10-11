using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.ViewModels;
using DevHub.ViewModels.Shell;

namespace DevHub.Actions.Providers;

/// <summary>"Find Action…" (Ctrl+Shift+A): opens the Find Action overlay.</summary>
/// <remarks>
/// The popup needs the ActionRegistry, and the registry is built from providers like this one,
/// so the popup is created on demand through a factory (<c>Func&lt;FindActionViewModel&gt;</c>)
/// instead of being injected; that breaks the loop.
/// </remarks>
public sealed class FindActionProvider(MainWindowViewModel shell, Func<FindActionViewModel> createPopup) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.FindAction, "Find Action…",
            new RelayCommand(() => shell.FindAction ??= createPopup()))
        {
            Category = "Help",
            IconKind = IconKind.Search,
            Gesture = "Ctrl+Shift+A",
            Menu = "Help",
            ToolbarOrder = 0,
            WelcomeOrder = 0,
            Description = "Search every action by name and run it.",
        };
    }
}
