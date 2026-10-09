namespace DevHub.Core.Settings;

/// <summary>
/// Everything DevHub persists between runs. Today just the per-language "open with" overrides;
/// more sections (author name, library location, …) land here later. Only user *overrides* are
/// stored — unset languages fall back to <see cref="Projects.DefaultToolPolicy"/> — so the file
/// stays small and readable.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Language id → chosen tool. Missing keys mean "use the default policy".</summary>
    public Dictionary<string, LanguageToolChoice> LanguageTools { get; set; } = new();

    /// <summary>
    /// Tool-window layout from the last session. Null until the first save, so the app can
    /// tell "never saved" (use its own defaults) from "user closed everything".
    /// </summary>
    public ToolWindowLayout? ToolWindows { get; set; }

    /// <summary>Open editor tabs from the last session; null until the first save.</summary>
    public EditorLayout? Editor { get; set; }

    /// <summary>Folders the Repositories tool window scans for git repositories.</summary>
    public List<string> RepositoryFolders { get; set; } = new();
}
