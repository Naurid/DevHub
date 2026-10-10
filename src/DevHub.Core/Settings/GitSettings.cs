namespace DevHub.Core.Settings;

/// <summary>Git client view preferences (all repository tabs share them).</summary>
public sealed class GitSettings
{
    /// <summary>Local Changes shows folders (Fork's tree view) rather than a flat file list.</summary>
    public bool ChangesAsTree { get; set; } = true;
}
