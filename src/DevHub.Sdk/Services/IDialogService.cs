namespace DevHub.Services;

/// <summary>
/// The seam between ViewModels and Avalonia's windowing/dialog APIs. ViewModels depend on
/// this interface only — they never touch <c>Window</c>, <c>TopLevel</c> or <c>StorageProvider</c>
/// directly, which keeps them testable and View-agnostic (a CLAUDE.md hard rule).
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a modal dialog (OK / Cancel frame around the dialog's View) and waits for it to
    /// close. Returns true for OK, false for Cancel / closed with the window's X.
    /// </summary>
    Task<bool> ShowDialogAsync(ViewModels.Dialogs.DialogViewModelBase dialog);

    /// <summary>Opens a folder picker and returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickFolderAsync(string title);

    /// <summary>
    /// Opens a file picker and returns the chosen path, or null if cancelled.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filterName">Display name of the file-type filter (e.g. "Programs").</param>
    /// <param name="patterns">Glob patterns for the filter (e.g. "*.exe").</param>
    Task<string?> PickFileAsync(string title, string filterName, IReadOnlyList<string> patterns);
}
