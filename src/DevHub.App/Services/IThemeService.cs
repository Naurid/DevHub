namespace DevHub.Services;

/// <summary>
/// Runtime appearance switches: Dark/Light, Compact density and the accent color.
/// ViewModels use this instead of touching <c>Application</c> resources themselves.
/// Not persisted yet; the Settings dialog (UI-0 step 9) will save and restore these.
/// </summary>
public interface IThemeService
{
    /// <summary>True for the Dark theme variant, false for Light.</summary>
    bool IsDark { get; set; }

    /// <summary>True when the Compact size overrides are active.</summary>
    bool IsCompact { get; set; }

    /// <summary>Current accent as "#RRGGBB".</summary>
    string Accent { get; }

    /// <summary>Changes the accent color; <paramref name="hex"/> is "#RRGGBB".</summary>
    void SetAccent(string hex);

    /// <summary>
    /// Raised after Dark/Light, Compact or the accent changes. Most UI follows automatically
    /// via DynamicResource; this is for things that can't (the window icon bitmap) and for
    /// ViewModels that mirror the state (the UI Gallery's switches).
    /// </summary>
    event EventHandler? Changed;
}
