using System.Windows.Input;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Actions;

/// <summary>
/// Something the user can do, described once and shown in many places: toolbar buttons,
/// menus, context menus, keyboard shortcuts and (UI-0 step 10) the Find Action popup.
/// Like an IntelliJ "AnAction".
/// </summary>
/// <remarks>
/// The behavior is an ordinary <see cref="ICommand"/>, usually one a ViewModel already has
/// (e.g. <c>RepositoriesViewModel.ScanCommand</c>). Enabled/disabled state therefore comes
/// from the command's <c>CanExecute</c>, and every button/menu item bound to the action
/// greys out together. Plain data + ICommand, no Avalonia types, so ViewModels may use it.
/// </remarks>
public sealed class AppAction
{
    public AppAction(string id, string text, ICommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(command);

        Id = id;
        Text = text;
        Command = command;
    }

    /// <summary>Stable id, "Area.Verb" style (e.g. "Git.Commit"). Used by menus, keymaps and settings.</summary>
    public string Id { get; }

    /// <summary>Short label for menus and buttons, e.g. "Rescan". Use "…" when it opens a dialog.</summary>
    public string Text { get; }

    /// <summary>What runs. Its CanExecute decides whether the action is enabled.</summary>
    public ICommand Command { get; }

    /// <summary>Group shown in Find Action, e.g. "Repositories" (English; shown translated: <see cref="CategoryText"/>).</summary>
    public string? Category { get; init; }

    /// <summary>The category in the user's language.</summary>
    public string? CategoryText => Category is null ? null : L.T(Category);

    /// <summary>Optional icon; one of <c>DevHub.Controls.IconKind</c>.</summary>
    public string? IconKind { get; init; }

    /// <summary>Optional longer explanation for tooltips and Find Action.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Keyboard shortcut in Avalonia KeyGesture syntax ("Ctrl+Shift+K", "F5", "Alt+1"), or null.
    /// Settable so a user keymap can override the default later (UI-0 step 9).
    /// </summary>
    public string? Gesture { get; set; }

    // ---- Where it appears (besides Find Action and its shortcut) -------------------------------
    // Orders: lowest first; a new hundred (0–99, 100–199, …) starts a new group, with a separator
    // in front. So "Commit" at 0 and "Discard" at 100 end up in separate groups.

    /// <summary>Top-level main-menu (☰) group it appears in: "File", "View", "Git", … Null = not in the main menu.</summary>
    public string? Menu { get; init; }

    /// <summary>Position inside its <see cref="Menu"/> group.</summary>
    public int MenuOrder { get; init; }

    /// <summary>Position on the title-bar toolbar; null = not on the toolbar.</summary>
    public int? ToolbarOrder { get; init; }

    /// <summary>Position in the Welcome view's list (shown with no tab open); null = not there.</summary>
    public int? WelcomeOrder { get; init; }

    /// <summary>"Category: Text", or just Text. What Find Action lists and searches.</summary>
    public string DisplayName => Category is null ? Text : $"{CategoryText}: {Text}";

    /// <summary>Tooltip text: the label plus the shortcut, e.g. "Rescan (F5)".</summary>
    public string ToolTip => Gesture is null ? Text : $"{Text} ({Gesture})";

    public override string ToString() => Id;
}
