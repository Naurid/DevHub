namespace DevHub.Sdk.Core.Settings;

/// <summary>Settings → Updates: whether DevHub looks for a new version when it starts.</summary>
public sealed class UpdateSettings
{
    /// <summary>Check GitHub Releases quietly a few seconds after start (nothing installs without a click).</summary>
    public bool CheckOnStartup { get; set; } = true;
}
