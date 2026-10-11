namespace DevHub.Sdk.Core.Settings;

/// <summary>
/// The editor area's tabs from the last session, so they reopen next launch.
/// Documents are referred to by id (e.g. "Templates"); unknown ids are skipped on restore.
/// </summary>
public sealed class EditorLayout
{
    /// <summary>Open tabs, left to right.</summary>
    public List<string> OpenDocuments { get; set; } = new();

    /// <summary>The selected tab, or null.</summary>
    public string? ActiveDocument { get; set; }
}
