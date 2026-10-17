using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.Sdk.Core.Localization;

namespace DevHub.ViewModels.Dialogs.Settings;

/// <summary>Settings → Appearance: language, theme (Dark / Light / follow Windows), accent color, Compact mode.</summary>
public partial class AppearanceSettingsPageViewModel : SettingsPageViewModelBase
{
    private readonly IThemeService _theme;
    private readonly ISettingsStore _store;
    private readonly IStatusService _status;

    public override string Title => L.T("Appearance");
    public override int Order => 0;
    public override string Keywords => L.T("language english french theme dark light system windows accent color colour compact density size");

    /// <summary>Choices for the language combo box: Windows' language, then each language in its own name.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
        [new(L.System, L.T("Same as Windows")), .. L.Languages.Select(l => new LanguageOption(l.Code, l.Name))];

    /// <summary>Choices for the theme combo box.</summary>
    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new(ThemeMode.Dark, L.T("Dark")),
        new(ThemeMode.Light, L.T("Light")),
        new(ThemeMode.System, L.T("Sync with Windows")),
    ];

    /// <summary>Accent presets shown as color swatches.</summary>
    public IReadOnlyList<AccentOption> AccentPresets { get; } =
    [
        new(L.T("Blue"), "#3574F0"),
        new(L.T("Green"), "#25A146"),
        new(L.T("Purple"), "#8C5CDB"),
        new(L.T("Orange"), "#E0802B"),
        new(L.T("Pink"), "#D6457E"),
        new(L.T("Teal"), "#21A0A0"),
    ];

    [ObservableProperty] private LanguageOption _selectedLanguage;
    [ObservableProperty] private ThemeOption _selectedTheme;
    [ObservableProperty] private AccentOption? _selectedPreset;

    /// <summary>Accent as "#RRGGBB"; editable directly for a custom color.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccentValid))]
    private string _accent;

    [ObservableProperty] private bool _isCompact;

    public bool IsAccentValid => HexColor().IsMatch(Accent);

    public AppearanceSettingsPageViewModel(IThemeService theme, ISettingsStore store, IStatusService status)
    {
        _theme = theme;
        _store = store;
        _status = status;
        var language = store.Load().Language ?? L.System;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == language) ?? Languages[0];
        // Initialise the backing fields directly so loading doesn't count as a user edit.
        _selectedTheme = Themes.First(t => t.Mode == theme.Mode);
        _accent = theme.Accent;
        _selectedPreset = AccentPresets.FirstOrDefault(p => p.Hex.Equals(theme.Accent, StringComparison.OrdinalIgnoreCase));
        _isCompact = theme.IsCompact;
    }

    partial void OnSelectedLanguageChanged(LanguageOption value) => MarkModified();
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
        var language = SelectedLanguage.Code == L.System ? null : SelectedLanguage.Code;
        if (language != _store.Load().Language)
        {
            _store.Update(s => s.Language = language);
            _status.Show(L.T("The language changes when DevHub restarts (File → Restart DevHub)."));
        }
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
public sealed record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

public sealed record ThemeOption(ThemeMode Mode, string Name)
{
    public override string ToString() => Name;
}

public sealed record AccentOption(string Name, string Hex)
{
    public override string ToString() => Name;
}
