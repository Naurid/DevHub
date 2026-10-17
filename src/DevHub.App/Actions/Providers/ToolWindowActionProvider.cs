using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.ViewModels.ToolWindows;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Actions.Providers;

/// <summary>
/// One "show/hide" action per tool window (Alt+1, Alt+2… in <see cref="ToolWindowViewModelBase.Order"/>,
/// like JetBrains' Alt+1 = Project), plus Hide All Tool Windows (Ctrl+Shift+F12). All in the View menu.
/// </summary>
public sealed class ToolWindowActionProvider(ToolWindowManager toolWindows) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        var number = 1;
        foreach (var toolWindow in toolWindows.All) // already in Order
        {
            var gesture = number <= 9 ? $"Alt+{number}" : null;

            // So the stripe button's tooltip can show the shortcut too.
            toolWindow.Shortcut = gesture;

            // From the stable Id, not the Title: renaming a tool window ("Repositories" → "Projects")
            // must not change its action id (the Welcome page and saved shortcuts refer to it).
            yield return new AppAction(ActionIds.ToolWindowPrefix + char.ToUpperInvariant(toolWindow.Id[0]) + toolWindow.Id[1..],
                toolWindow.Title, toolWindow.ToggleCommand!)
            {
                Category = "Tool Windows",
                IconKind = toolWindow.IconKind,
                Gesture = gesture,
                Menu = "View",
                MenuOrder = number - 1,
                WelcomeOrder = toolWindow.WelcomeOrder,
                Description = L.T($"Show or hide the {toolWindow.Title} tool window."),
            };
            number++;
        }

        yield return new AppAction(ActionIds.HideAllToolWindows, L.T("Hide All Tool Windows"),
            new RelayCommand(toolWindows.HideAll))
        {
            Category = "Tool Windows",
            Gesture = "Ctrl+Shift+F12",
            Menu = "View",
            MenuOrder = 100,
        };
    }
}
