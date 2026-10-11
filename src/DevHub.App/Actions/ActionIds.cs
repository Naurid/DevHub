namespace DevHub.Actions;

/// <summary>
/// Ids of the shell's own actions, so C# code gets autocomplete and typo checks. XAML uses the
/// same strings: <c>{a:Action View.ToggleTheme}</c>. Each plugin has its own ids class
/// (e.g. <c>GitActionIds</c>).
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

    // App
    public const string OpenSettings = "App.Settings";
    public const string FindAction = "App.FindAction";

    // Tool windows
    /// <summary>Prefix for "show/hide tool window" actions; the tool window's Id follows, capitalised: "ToolWindow.Repositories" (the Projects tool window).</summary>
    public const string ToolWindowPrefix = "ToolWindow.";
    public const string HideAllToolWindows = "ToolWindow.HideAll";
}
