using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace DevHub.Controls;

/// <summary>
/// Clicking empty space in a list or a tree (beside or below its rows) clears its selection, everywhere in the app,
/// plugins included. Lists that must always show a selection (the tab strip, a settings page list, a sidebar that
/// drives the view) opt out with Avalonia's own <c>SelectionMode="Single,AlwaysSelected"</c>.
/// </summary>
public static class ClickAwayDeselect
{
    private static bool _installed;

    /// <summary>Hooks every <see cref="ListBox"/> and <see cref="TreeView"/> (the app calls this once at start-up).</summary>
    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;
        InputElement.PointerPressedEvent.AddClassHandler<ListBox>(OnListPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<TreeView>(OnTreePressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void OnListPressed(ListBox list, PointerPressedEventArgs e)
    {
        if (!IsEmptyClick(list, e) || list.SelectionMode.HasFlag(SelectionMode.AlwaysSelected) || list.Classes.Contains("tabs")
            || (e.Source as Visual)?.FindAncestorOfType<ListBox>(includeSelf: true) != list)
            return;
        if (list.SelectedItems is { Count: > 0 } items)
            items.Clear();
        list.SelectedItem = null;
    }

    private static void OnTreePressed(TreeView tree, PointerPressedEventArgs e)
    {
        if (!IsEmptyClick(tree, e) || tree.SelectionMode.HasFlag(SelectionMode.AlwaysSelected)
            || (e.Source as Visual)?.FindAncestorOfType<TreeView>(includeSelf: true) != tree)
            return;
        if (tree.SelectedItems is { Count: > 0 } items)
            items.Clear();
        tree.SelectedItem = null;
    }

    /// <summary>A plain left press that isn't on a row, a scroll bar or a control inside the list.</summary>
    private static bool IsEmptyClick(Control control, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed || e.KeyModifiers != KeyModifiers.None
            || e.Source is not Visual source)
            return false;
        return source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is null
               && source.FindAncestorOfType<TreeViewItem>(includeSelf: true) is null
               && source.FindAncestorOfType<ScrollBar>(includeSelf: true) is null
               && source.FindAncestorOfType<Button>(includeSelf: true) is null
               && source.FindAncestorOfType<TextBox>(includeSelf: true) is null;
    }
}
