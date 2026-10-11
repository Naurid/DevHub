namespace DevHub.Services;

/// <summary>Which theme to use. <see cref="System"/> follows the Windows light/dark setting.</summary>
public enum ThemeMode
{
    Dark,
    Light,
    System,
}

/// <summary>
/// Runtime appearance switches: theme, Compact density and the accent color.
/// ViewModels use this instead of touching <c>Application</c> resources themselves.
/// Every change is saved to settings and restored at the next launch.
/// </summary>
public interface IThemeService
{
    /// <summary>The chosen theme mode (Settings → Appearance).</summary>
    ThemeMode Mode { get; set; }

    /// <summary>True when the theme currently showing is dark. Setting it picks Dark or Light explicitly.</summary>
    bool IsDark { get; set; }

    /// <summary>True when the Compact size overrides are active.</summary>
    bool IsCompact { get; set; }

    /// <summary>Current accent as "#RRGGBB".</summary>
    string Accent { get; }

    /// <summary>Changes the accent color; <paramref name="hex"/> is "#RRGGBB".</summary>
    void SetAccent(string hex);

    /// <summary>
    /// Raised after the theme, Compact or the accent changes. Most UI follows automatically
    /// via DynamicResource; this is for things that can't (the window icon bitmap) and for
    /// ViewModels that mirror the state (the UI Gallery's switches, the status bar).
    /// </summary>
    event EventHandler? Changed;
}
