using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Services;

namespace DevHub.ViewModels.Dialogs.Settings;

/// <summary>Settings → Appearance: theme (Dark / Light / follow Windows), accent color, Compact mode.</summary>
public partial class AppearanceSettingsPageViewModel : SettingsPageViewModelBase
{
    private readonly IThemeService _theme;

    public override string Title => "Appearance";
    public override string Keywords => "theme dark light system windows accent color colour compact density size";

    /// <summary>Choices for the theme combo box.</summary>
    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new(ThemeMode.Dark, "Dark"),
        new(ThemeMode.Light, "Light"),
        new(ThemeMode.System, "Sync with Windows"),
    ];

    /// <summary>Accent presets shown as color swatches.</summary>
    public IReadOnlyList<AccentOption> AccentPresets { get; } =
    [
        new("Blue", "#3574F0"),
        new("Green", "#25A146"),
        new("Purple", "#8C5CDB"),
        new("Orange", "#E0802B"),
        new("Pink", "#D6457E"),
        new("Teal", "#21A0A0"),
    ];

    [ObservableProperty] private ThemeOption _selectedTheme;
    [ObservableProperty] private AccentOption? _selectedPreset;

    /// <summary>Accent as "#RRGGBB"; editable directly for a custom color.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccentValid))]
    private string _accent;

    [ObservableProperty] private bool _isCompact;

    public bool IsAccentValid => HexColor().IsMatch(Accent);

    public AppearanceSettingsPageViewModel(IThemeService theme)
    {
        _theme = theme;
        // Initialise the backing fields directly so loading doesn't count as a user edit.
        _selectedTheme = Themes.First(t => t.Mode == theme.Mode);
        _accent = theme.Accent;
        _selectedPreset = AccentPresets.FirstOrDefault(p => p.Hex.Equals(theme.Accent, StringComparison.OrdinalIgnoreCase));
        _isCompact = theme.IsCompact;
    }

    partial void OnSelectedThemeChanged(ThemeOption value) => MarkModified();
    partial void OnIsCompactChanged(bool value) => MarkModified();

    partial void OnSelectedPresetChanged(AccentOption? value)
    {
        if (value is not null)
            Accent = value.Hex; // → OnAccentChanged marks modified
    }

    partial void OnAccentChanged(string value)
    {
        // Keep the swatch selection in step with a typed-in color.
        SelectedPreset = AccentPresets.FirstOrDefault(p => p.Hex.Equals(value, StringComparison.OrdinalIgnoreCase));
        MarkModified();
    }

    public override void Apply()
    {
        _theme.Mode = SelectedTheme.Mode;
        _theme.IsCompact = IsCompact;
        if (IsAccentValid)
            _theme.SetAccent(Accent);
        IsModified = false;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}

// ToString = what screen readers announce for a list/combo item that has no explicit name.
public sealed record ThemeOption(ThemeMode Mode, string Name)
{
    public override string ToString() => Name;
}

public sealed record AccentOption(string Name, string Hex)
{
    public override string ToString() => Name;
}
