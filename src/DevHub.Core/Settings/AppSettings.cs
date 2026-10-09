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
}
