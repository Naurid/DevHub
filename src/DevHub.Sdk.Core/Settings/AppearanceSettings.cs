namespace DevHub.Sdk.Core.Settings;

/// <summary>
/// Look-and-feel choices from Settings → Appearance (and the quick toggles): theme, accent
/// color and density. Stored as plain strings so Core stays free of UI types.
/// </summary>
public sealed class AppearanceSettings
{
    public const string ThemeDark = "dark";
    public const string ThemeLight = "light";
    public const string ThemeSystem = "system";

    /// <summary>"dark", "light" or "system" (follow Windows).</summary>
    public string Theme { get; set; } = ThemeDark;

    /// <summary>Accent color as "#RRGGBB".</summary>
    public string Accent { get; set; } = "#3574F0";

    /// <summary>Compact density (smaller rows, toolbars and paddings).</summary>
    public bool Compact { get; set; }
}
