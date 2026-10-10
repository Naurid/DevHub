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

    // App
    public const string OpenSettings = "App.Settings";
    public const string FindAction = "App.FindAction";

    // Repositories
    public const string AddScanFolder = "Repositories.AddFolder";
    public const string RescanRepositories = "Repositories.Rescan";

    // Git (act on the active repository tab)
    public const string GitRefresh = "Git.Refresh";
    public const string GitCommit = "Git.Commit";
    public const string GitDiscard = "Git.Discard";
    public const string GitBranches = "Git.Branches";
    public const string GitNewBranch = "Git.NewBranch";
    public const string GitFetch = "Git.Fetch";
    public const string GitPull = "Git.Pull";
    public const string GitPush = "Git.Push";
    public const string GitForcePush = "Git.ForcePush";
    public const string GitStash = "Git.Stash";
    public const string GitUnstash = "Git.Unstash";

    // Tool windows
    /// <summary>Prefix for "show/hide tool window" actions; the title follows, e.g. "ToolWindow.Repositories".</summary>
    public const string ToolWindowPrefix = "ToolWindow.";
    public const string HideAllToolWindows = "ToolWindow.HideAll";
}
