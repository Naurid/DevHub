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
        // Tool-window actions are generated per tool window, so collect them by id prefix.
        var toolWindows = registry.All
            .Where(a => a.Id.StartsWith(ActionIds.ToolWindowPrefix, StringComparison.Ordinal)
                        && a.Id != ActionIds.HideAllToolWindows)
            .Select(a => a.Id);

        // The UI Gallery document only exists in Debug builds.
        var gallery = registry.TryGet(ActionIds.NavigatePrefix + "UiGallery", out _)
            ? [MenuBuilder.Separator, ActionIds.NavigatePrefix + "UiGallery"]
            : Array.Empty<string>();

        return
        [
            Group(registry, "File",
            [
                ActionIds.NavigatePrefix + "Generate", ActionIds.NavigatePrefix + "Import",
                MenuBuilder.Separator, ActionIds.NavigatePrefix + "Templates",
                MenuBuilder.Separator, ActionIds.OpenSettings,
            ]),
            Group(registry, "View",
            [
                .. toolWindows, MenuBuilder.Separator, ActionIds.HideAllToolWindows,
                MenuBuilder.Separator, ActionIds.NextTab, ActionIds.PreviousTab, ActionIds.CloseTab,
                MenuBuilder.Separator, ActionIds.ToggleTheme, ActionIds.ToggleCompact,
                .. gallery,
            ]),
            Group(registry, "Repositories", [ActionIds.AddScanFolder, MenuBuilder.Separator, ActionIds.RescanRepositories]),
            Group(registry, "Git", [ActionIds.GitCommit, ActionIds.GitDiscard, ActionIds.GitBranches, ActionIds.GitNewBranch, ActionIds.GitRefresh]),
            Group(registry, "Help", [ActionIds.FindAction]),
        ];
    }

    private static MenuItem Group(ActionRegistry registry, string header, IEnumerable<string> ids) => new()
    {
        Header = header,
        ItemsSource = MenuBuilder.Build(registry, ids),
    };
}
