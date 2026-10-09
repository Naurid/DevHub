using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.ViewModels.ToolWindows;

namespace DevHub.Actions.Providers;

/// <summary>
/// One "show/hide" action per tool window (Alt+1, Alt+2… in registration order, like
/// JetBrains' Alt+1 = Project), plus Hide All Tool Windows (Ctrl+Shift+F12).
/// </summary>
public sealed class ToolWindowActionProvider(ToolWindowManager toolWindows) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        var number = 1;
        foreach (var toolWindow in toolWindows.All)
        {
            var gesture = number <= 9 ? $"Alt+{number}" : null;
            number++;

            // So the stripe button's tooltip can show the shortcut too.
            toolWindow.Shortcut = gesture;

            yield return new AppAction(ActionIds.ToolWindowPrefix + toolWindow.Title.Replace(" ", ""),
                toolWindow.Title, toolWindow.ToggleCommand!)
            {
                Category = "Tool Windows",
                IconKind = toolWindow.IconKind,
                Gesture = gesture,
                Description = $"Show or hide the {toolWindow.Title} tool window.",
            };
        }

        yield return new AppAction(ActionIds.HideAllToolWindows, "Hide All Tool Windows",
            new RelayCommand(toolWindows.HideAll))
        {
            Category = "Tool Windows",
            Gesture = "Ctrl+Shift+F12",
        };
    }
}
