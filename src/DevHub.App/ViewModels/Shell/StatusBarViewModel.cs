using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Services;

namespace DevHub.ViewModels.Shell;

/// <summary>
/// The status bar at the bottom of the window: a message on the left (set through
/// <see cref="IStatusService"/>) and small widgets on the right showing the current theme
/// and density. Clicking a widget runs the matching action (wired in the View).
/// </summary>
/// <remarks>
/// It is also the <see cref="IStatusService"/> implementation: one singleton, registered
/// under both types, so whatever a page shows ends up in <see cref="Message"/>.
/// It must not depend on the ActionRegistry: the registry's providers depend on
/// MainWindowViewModel, which depends on this (that would be a DI cycle).
/// </remarks>
public partial class StatusBarViewModel : ViewModelBase, IStatusService, IDisposable
{
    private readonly IThemeService _theme;

    [ObservableProperty] private string? _message;
    [ObservableProperty] private string _themeLabel = "";
    [ObservableProperty] private string _densityLabel = "";

    public StatusBarViewModel(IThemeService theme)
    {
        _theme = theme;
        UpdateAppearance();
        // Unsubscribed in Dispose (the DI container disposes singletons at shutdown).
        _theme.Changed += OnThemeChanged;
    }

    public void Show(string? message) => Message = string.IsNullOrWhiteSpace(message) ? null : message;

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateAppearance();

    private void UpdateAppearance()
    {
        ThemeLabel = _theme.IsDark ? "Dark" : "Light";
        DensityLabel = _theme.IsCompact ? "Compact" : "Normal";
    }

    public void Dispose() => _theme.Changed -= OnThemeChanged;
}
