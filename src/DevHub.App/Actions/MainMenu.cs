using Avalonia.Controls;

namespace DevHub.Actions;

/// <summary>
/// The main menu (the ☰ button in the title bar): one submenu per <see cref="AppAction.Menu"/>
/// group, its items arranged by <see cref="AppAction.MenuOrder"/> (a new hundred = a separator).
/// So a plugin adds menu items by setting <c>Menu</c> on its actions, and a switched-off plugin's
/// items (and an emptied group) simply aren't there. In XAML: <c>&lt;MenuFlyout ItemsSource="{a:MainMenu}" /&gt;</c>.
/// </summary>
public static class MainMenu
{
    /// <summary>Groups that always sit at the start, in this order; Help is always last.</summary>
    private static readonly string[] Leading = ["File", "View"];
    private const string Last = "Help";

    public static IReadOnlyList<Control> Build(ActionRegistry registry)
    {
        // Plugin groups ("Projects", "Git", …) come in the order their first action was
        // registered, which follows the plugins' dependency order.
        var groups = registry.All
            .Select(a => a.Menu)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(g => g == Last ? int.MaxValue : Array.IndexOf(Leading, g) is var i and >= 0 ? i : Leading.Length)
            .ToList();

        return groups
            .Select(group => (Control)new MenuItem
            {
                Header = group,
                ItemsSource = MenuBuilder.Build(ActionLayout.Arrange(
                    registry.All.Where(a => a.Menu == group), a => a.MenuOrder)),
            })
            .ToList();
    }
}
