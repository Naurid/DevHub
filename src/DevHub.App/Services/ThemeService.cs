using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace DevHub.Services;

/// <summary>
/// Applies appearance changes by editing <see cref="Application"/> resources. Everything
/// in the UI reads colors/sizes through DynamicResource, so changing a resource here
/// updates every control that uses it immediately.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private static readonly Uri CompactSizesUri =
        new("avares://DevHub.App/Themes/Tokens/Sizes.Compact.axaml");

    /// <summary>The loaded Sizes.Compact dictionary while Compact is on, else null.</summary>
    private IResourceProvider? _compactSizes;

    private static Application App =>
        Application.Current ?? throw new InvalidOperationException("Avalonia is not initialized.");

    public bool IsDark
    {
        get => App.ActualThemeVariant == ThemeVariant.Dark;
        set => App.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public bool IsCompact
    {
        get => _compactSizes is not null;
        set
        {
            if (value == IsCompact)
                return;

            var merged = App.Resources.MergedDictionaries;
            if (value)
            {
                // Added last, so its keys win over Sizes.axaml (later merged dictionaries win).
                _compactSizes = (IResourceProvider)AvaloniaXamlLoader.Load(CompactSizesUri);
                merged.Add(_compactSizes);
            }
            else
            {
                merged.Remove(_compactSizes!);
                _compactSizes = null;
            }

            // Fluent has its own density switch for the controls we don't size ourselves.
            if (App.Styles.OfType<FluentTheme>().FirstOrDefault() is { } fluent)
                fluent.DensityStyle = value ? DensityStyle.Compact : DensityStyle.Normal;
        }
    }

    public string Accent { get; private set; } = "";

    /// <summary>
    /// Call once at startup: pushes Color.Accent from Colors.axaml into Fluent's accent keys.
    /// </summary>
    public void Initialize()
    {
        var accent = App.TryGetResource("Color.Accent", null, out var value) && value is Color c
            ? c
            : Color.Parse("#3574F0");
        ApplyAccent(accent);
    }

    public void SetAccent(string hex) => ApplyAccent(Color.Parse(hex));

    private void ApplyAccent(Color accent)
    {
        var resources = App.Resources;

        // Our tokens. Setting them on Application.Resources itself overrides the
        // values from the merged Colors.axaml (own entries are checked first).
        resources["Color.Accent"] = accent;
        resources["Color.Accent.Hover"] = Shade(accent, -0.06);
        resources["Color.Accent.Pressed"] = Shade(accent, -0.12);

        // Fluent's accent family: checked boxes, toggle switches, sliders, focus borders…
        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorDark1"] = Shade(accent, -0.06);
        resources["SystemAccentColorDark2"] = Shade(accent, -0.12);
        resources["SystemAccentColorDark3"] = Shade(accent, -0.18);
        resources["SystemAccentColorLight1"] = Shade(accent, 0.06);
        resources["SystemAccentColorLight2"] = Shade(accent, 0.12);
        resources["SystemAccentColorLight3"] = Shade(accent, 0.18);

        Accent = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
    }

    /// <summary>Lightens (positive) or darkens (negative) a color by changing HSL lightness.</summary>
    private static Color Shade(Color color, double amount)
    {
        var hsl = color.ToHsl();
        var lightness = Math.Clamp(hsl.L + amount, 0, 1);
        return new HslColor(hsl.A, hsl.H, hsl.S, lightness).ToRgb();
    }
}
