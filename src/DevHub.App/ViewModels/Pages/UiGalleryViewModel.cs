using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Services;

namespace DevHub.ViewModels.Pages;

/// <summary>
/// Debug-only page showing every token and stock control in the current theme, with
/// switches for Dark/Light, Compact and accent. Use it to check design-system changes.
/// </summary>
public partial class UiGalleryViewModel : PageViewModelBase, IDisposable
{
    private readonly IThemeService _theme;

    public override string Title => "UI Gallery";
    public override string Icon => IconKind.Palette;

    [ObservableProperty] private bool _isDark;
    [ObservableProperty] private bool _isCompact;
    [ObservableProperty] private string _accent;

    /// <summary>Every vendored icon name.</summary>
    public IReadOnlyList<string> IconKinds => IconKind.All;

    /// <summary>Sample rows for the list/combo demos.</summary>
    public IReadOnlyList<string> SampleItems { get; } =
        ["DevHub", "Timber (Unity)", "website (Tauri)", "scripts (Python)", "a-very-long-repository-name-to-test-trimming"];

    public UiGalleryViewModel(IThemeService theme)
    {
        _theme = theme;
        _isDark = theme.IsDark;
        _isCompact = theme.IsCompact;
        _accent = theme.Accent;

        // The theme can also change from elsewhere (e.g. the Ctrl+Alt+D action);
        // keep the switches in sync. Unsubscribed in Dispose.
        _theme.Changed += OnThemeChanged;
    }

    // Setting a property to the value it already has does nothing, and ThemeService ignores
    // no-op changes, so these two directions can't loop.
    partial void OnIsDarkChanged(bool value) => _theme.IsDark = value;

    partial void OnIsCompactChanged(bool value) => _theme.IsCompact = value;

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        IsDark = _theme.IsDark;
        IsCompact = _theme.IsCompact;
        Accent = _theme.Accent;
    }

    [RelayCommand]
    private void SetAccent(string hex) => _theme.SetAccent(hex);

    /// <summary>Called by the DI container when it is disposed (app shutdown).</summary>
    public void Dispose() => _theme.Changed -= OnThemeChanged;
}
