using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Services;

namespace DevHub.ViewModels.Pages;

/// <summary>
/// Debug-only page showing every token and stock control in the current theme, with
/// switches for Dark/Light, Compact and accent. Use it to check design-system changes.
/// </summary>
public partial class UiGalleryViewModel : PageViewModelBase
{
    private readonly IThemeService _theme;

    public override string Title => "UI Gallery";
    public override string Icon => ((char)0xE790).ToString(); // Segoe MDL2: color

    [ObservableProperty] private bool _isDark;
    [ObservableProperty] private bool _isCompact;
    [ObservableProperty] private string _accent;

    /// <summary>Sample rows for the list/combo demos.</summary>
    public IReadOnlyList<string> SampleItems { get; } =
        ["DevHub", "Timber (Unity)", "website (Tauri)", "scripts (Python)", "a-very-long-repository-name-to-test-trimming"];

    public UiGalleryViewModel(IThemeService theme)
    {
        _theme = theme;
        _isDark = theme.IsDark;
        _isCompact = theme.IsCompact;
        _accent = theme.Accent;
    }

    partial void OnIsDarkChanged(bool value) => _theme.IsDark = value;

    partial void OnIsCompactChanged(bool value) => _theme.IsCompact = value;

    [RelayCommand]
    private void SetAccent(string hex)
    {
        _theme.SetAccent(hex);
        Accent = _theme.Accent;
    }
}
