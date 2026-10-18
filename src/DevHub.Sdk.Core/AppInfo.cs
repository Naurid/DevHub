namespace DevHub.Sdk.Core;

/// <summary>
/// Left over from the M0 skeleton and used by nothing: the Templates plugin has its own schema version
/// (<c>TemplateManifest.CurrentSchemaVersion</c>). Removing a public type is a breaking Sdk change, so it goes with
/// the next plugin API version (<c>PluginApi.Version</c>), together with whatever else needs that bump.
/// </summary>
[Obsolete("Unused: Templates has its own TemplateManifest.CurrentSchemaVersion. Removed with the next plugin API version.")]
public static class AppInfo
{
    /// <summary>Human-readable product name.</summary>
    public const string Name = "DevHub";

    /// <summary>Not the template schema version Templates checks (that's TemplateManifest.CurrentSchemaVersion).</summary>
    public const int TemplateSchemaVersion = 1;
}
