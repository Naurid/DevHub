using Avalonia.Controls;
using Avalonia.Input;
using DevHub.Controls;

namespace DevHub.Actions;

/// <summary>
/// Builds menu items from action ids, e.g. <c>["Repositories.Rescan", "-", "View.ToggleTheme"]</c>.
/// "-" is a separator; leading, trailing and doubled separators are dropped, so lists can be
/// composed without worrying about edges. Each item shows the action's text, icon and
/// shortcut, runs its command, and greys out when the command can't execute.
/// In XAML use <see cref="ActionMenuExtension"/>.
/// </summary>
public static class MenuBuilder
{
    public const string Separator = "-";

    public static IReadOnlyList<Control> Build(ActionRegistry registry, IEnumerable<string> ids)
    {
        var items = new List<Control>();
        var pendingSeparator = false;

        foreach (var id in ids)
        {
            if (id == Separator)
            {
                // Only emit it once a real item follows (drops leading/trailing/double).
                pendingSeparator = items.Count > 0;
                continue;
            }

            if (pendingSeparator)
            {
                items.Add(new Avalonia.Controls.Separator());
                pendingSeparator = false;
            }

            items.Add(CreateItem(registry.Get(id)));
        }

        return items;
    }

    private static MenuItem CreateItem(AppAction action) => new()
    {
        Header = action.Text,
        Command = action.Command,
        InputGesture = string.IsNullOrWhiteSpace(action.Gesture) ? null : Gestures.Parse(action.Gesture),
        Icon = action.IconKind is { } kind ? new Icon { Kind = kind } : null,
    };
}
