using Avalonia.Controls;

namespace DevHub.Actions;

/// <summary>
/// The main menu (the ☰ button in the title bar): top-level groups, each a submenu built
/// from action ids by <see cref="MenuBuilder"/>. Add an action to a menu by adding its id
/// here. In XAML: <c>&lt;MenuFlyout ItemsSource="{a:MainMenu}" /&gt;</c>.
/// </summary>
public static class MainMenu
{
    public static IReadOnlyList<Control> Build(ActionRegistry registry)
    {
        // Navigation and tool-window actions are generated per page / tool window, so collect
        // them by id prefix.
        var navigate = registry.All
            .Where(a => a.Id.StartsWith(ActionIds.NavigatePrefix, StringComparison.Ordinal))
            .Select(a => a.Id);
        var toolWindows = registry.All
            .Where(a => a.Id.StartsWith(ActionIds.ToolWindowPrefix, StringComparison.Ordinal)
                        && a.Id != ActionIds.HideAllToolWindows)
            .Select(a => a.Id);

        return
        [
            Group(registry, "View",
                [.. toolWindows, MenuBuilder.Separator, ActionIds.HideAllToolWindows,
                 MenuBuilder.Separator, ActionIds.ToggleTheme, ActionIds.ToggleCompact]),
            Group(registry, "Navigate", navigate),
            Group(registry, "Repositories", [ActionIds.AddScanFolder, MenuBuilder.Separator, ActionIds.RescanRepositories]),
        ];
    }

    private static MenuItem Group(ActionRegistry registry, string header, IEnumerable<string> ids) => new()
    {
        Header = header,
        ItemsSource = MenuBuilder.Build(registry, ids),
    };
}
