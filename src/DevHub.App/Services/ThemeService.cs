using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using DevHub.Sdk.Core.Settings;

namespace DevHub.Services;

/// <summary>
/// Applies appearance changes by editing <see cref="Application"/> resources. Everything
/// in the UI reads colors/sizes through DynamicResource, so changing a resource here
/// updates every control that uses it immediately.
/// </summary>
/// <remarks>
/// Every change is also saved (settings → <see cref="AppearanceSettings"/>), whoever made it:
/// the Settings dialog, Ctrl+Alt+D, the status-bar widgets or the UI Gallery. One place to
/// persist means they can never disagree.
/// </remarks>
public sealed class ThemeService(ISettingsStore store) : IThemeService
{
    private static readonly Uri CompactSizesUri =
        new("avares://DevHub.App/Themes/Tokens/Sizes.Compact.axaml");

    /// <summary>The loaded Sizes.Compact dictionary while Compact is on, else null.</summary>
    private IResourceProvider? _compactSizes;

    /// <summary>True while <see cref="Initialize"/> restores saved values (don't save them back).</summary>
    private bool _restoring;

    private ThemeMode _mode = ThemeMode.Dark;

    private static Application App =>
        Application.Current ?? throw new InvalidOperationException("Avalonia is not initialized.");

    public ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (value == _mode && App.RequestedThemeVariant == ToVariant(value))
                return;

            _mode = value;
            // Default = "no preference", which makes Avalonia follow the Windows setting.
            App.RequestedThemeVariant = ToVariant(value);
            OnChanged();
        }
    }

    public bool IsDark
    {
        get => App.ActualThemeVariant == ThemeVariant.Dark;
        // Toggling picks an explicit mode (leaves "follow Windows").
        set
        {
            if (value != IsDark)
                Mode = value ? ThemeMode.Dark : ThemeMode.Light;
        }
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

            OnChanged();
        }
    }

    public string Accent { get; private set; } = "";

    /// <summary>The current accent as a Color, for things that can't use resources (window icon).</summary>
    public Color AccentColor { get; private set; }

    public event EventHandler? Changed;

    /// <summary>
    /// Call once at startup, before any window exists: restores the saved appearance, or, on
    /// first launch, keeps App.axaml's theme and Colors.axaml's accent.
    /// </summary>
    public void Initialize()
    {
        _restoring = true;
        try
        {
            _mode = App.RequestedThemeVariant == ThemeVariant.Light ? ThemeMode.Light
                : App.RequestedThemeVariant == ThemeVariant.Dark ? ThemeMode.Dark
                : ThemeMode.System;

            var defaultAccent = App.TryGetResource("Color.Accent", null, out var value) && value is Color c
                ? c
                : Color.Parse("#3574F0");

            if (store.Load().Appearance is { } saved)
            {
                Mode = FromSetting(saved.Theme);
                IsCompact = saved.Compact;
                ApplyAccent(Color.TryParse(saved.Accent, out var accent) ? accent : defaultAccent);
            }
            else
            {
                ApplyAccent(defaultAccent);
            }
        }
        finally
        {
            _restoring = false;
        }
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
        AccentColor = accent;
        OnChanged();
    }

    private void OnChanged()
    {
        if (!_restoring)
        {
            var appearance = new AppearanceSettings
            {
                Theme = ToSetting(_mode),
                Accent = Accent,
                Compact = IsCompact,
            };
            store.Update(s => s.Appearance = appearance);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static ThemeVariant ToVariant(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => ThemeVariant.Dark,
        ThemeMode.Light => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };

    private static ThemeMode FromSetting(string? theme) => theme switch
    {
        AppearanceSettings.ThemeLight => ThemeMode.Light,
        AppearanceSettings.ThemeSystem => ThemeMode.System,
        _ => ThemeMode.Dark,
    };

    private static string ToSetting(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => AppearanceSettings.ThemeLight,
        ThemeMode.System => AppearanceSettings.ThemeSystem,
        _ => AppearanceSettings.ThemeDark,
    };

    /// <summary>Lightens (positive) or darkens (negative) a color by changing HSL lightness.</summary>
    private static Color Shade(Color color, double amount)
    {
        var hsl = color.ToHsl();
        var lightness = Math.Clamp(hsl.L + amount, 0, 1);
        return new HslColor(hsl.A, hsl.H, hsl.S, lightness).ToRgb();
    }
}
