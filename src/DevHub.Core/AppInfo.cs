namespace DevHub.Core;

/// <summary>
/// Placeholder marker type for the M0 skeleton so the test project has something
/// real to assert against. Real engine types (template manifest model, renderer,
/// generator) arrive in later milestones — see docs/ROADMAP.md.
/// </summary>
public static class AppInfo
{
    /// <summary>Human-readable product name.</summary>
    public const string Name = "DevHub";

    /// <summary>Template manifest schema version this build understands.</summary>
    public const int TemplateSchemaVersion = 1;
}
