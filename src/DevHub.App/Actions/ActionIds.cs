namespace DevHub.Actions;

/// <summary>
/// Ids of all actions, so C# code gets autocomplete and typo checks. XAML uses the same
/// strings: <c>{a:Action View.ToggleTheme}</c>.
/// </summary>
public static class ActionIds
{
    // View
    public const string ToggleTheme = "View.ToggleTheme";
    public const string ToggleCompact = "View.ToggleCompact";

    /// <summary>Prefix for "open document" actions; the document Id follows, e.g. "Navigate.Templates".</summary>
    public const string NavigatePrefix = "Navigate.";

    // Editor tabs
    public const string CloseTab = "Editor.CloseTab";
    public const string NextTab = "Editor.NextTab";
    public const string PreviousTab = "Editor.PreviousTab";

    // Repositories
    public const string AddScanFolder = "Repositories.AddFolder";
    public const string RescanRepositories = "Repositories.Rescan";

    // Tool windows
    /// <summary>Prefix for "show/hide tool window" actions; the title follows, e.g. "ToolWindow.Repositories".</summary>
    public const string ToolWindowPrefix = "ToolWindow.";
    public const string HideAllToolWindows = "ToolWindow.HideAll";
}
