using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.ViewModels.Documents;
using DevHub.ViewModels.Shell;
using DevHub.ViewModels.ToolWindows;

namespace DevHub.ViewModels;

/// <summary>
/// The shell's DataContext: the editor area (document tabs) in the center, the tool windows
/// docked around it, the status bar, the plugins' title-bar widgets, and the Find Action overlay
/// while it's open. The title bar and menus get their behaviour from actions, not from here.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    /// <summary>The window's own minimum width (what every screen is made to work at).</summary>
    public const double BaseMinWidth = 720;

    // Beside the editor area: both tool-window stripes (Size.StripeWidth, the larger of Normal and Compact) and the
    // splitters of the side slots.
    private const double Chrome = 2 * 40 + 8;

    /// <param name="titleBarWidgets">Widgets the loaded plugins contribute to the title bar.</param>
    /// <param name="statusBarWidgets">Widgets the loaded plugins contribute to the status bar.</param>
    public MainWindowViewModel(DocumentManager documents, ToolWindowManager toolWindows, StatusBarViewModel statusBar,
        IEnumerable<TitleBarWidgetViewModelBase> titleBarWidgets, IEnumerable<StatusBarWidgetViewModelBase> statusBarWidgets)
    {
        Documents = documents;
        ToolWindows = toolWindows;
        StatusBar = statusBar;
        TitleBarWidgets = titleBarWidgets.OrderBy(w => w.Order).ToList();
        // Handed to the status bar here rather than injected into it: the status bar is also the
        // IStatusService, which widgets may need (that would be a DI cycle).
        StatusBar.Widgets = statusBarWidgets.OrderBy(w => w.Order).ToList();
        Documents.PropertyChanged += OnLayoutChanged;
        ToolWindows.PropertyChanged += OnLayoutChanged;
    }

    /// <summary>
    /// How narrow the window may get: <see cref="BaseMinWidth"/>, or wider when the tab in front needs a wider editor
    /// area (<see cref="DocumentViewModelBase.MinWidth"/>) next to the open side tool windows.
    /// </summary>
    public double WindowMinWidth
    {
        get
        {
            if (Documents.Active is not { MinWidth: > 0 } document)
                return BaseMinWidth;
            var sides = (ToolWindows.Left is null ? 0 : ToolWindows.LeftWidth) + (ToolWindows.Right is null ? 0 : ToolWindows.RightWidth);
            return Math.Max(BaseMinWidth, Math.Ceiling(Chrome + sides + document.MinWidth));
        }
    }

    private void OnLayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentManager.Active) or nameof(ToolWindowManager.Left) or nameof(ToolWindowManager.Right)
            or nameof(ToolWindowManager.LeftWidth) or nameof(ToolWindowManager.RightWidth))
            OnPropertyChanged(nameof(WindowMinWidth));
    }

    public void Dispose()
    {
        Documents.PropertyChanged -= OnLayoutChanged;
        ToolWindows.PropertyChanged -= OnLayoutChanged;
    }

    /// <summary>Open tabs in the center, or the Welcome view when there are none.</summary>
    public DocumentManager Documents { get; }

    /// <summary>Tool windows docked around the editor area (stripes + slots).</summary>
    public ToolWindowManager ToolWindows { get; }

    /// <summary>The status bar at the bottom of the window.</summary>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>Plugin widgets after the breadcrumb in the title bar (e.g. Git's branch widget).</summary>
    public IReadOnlyList<TitleBarWidgetViewModelBase> TitleBarWidgets { get; }

    /// <summary>
    /// The Find Action popup (Ctrl+Shift+A) while open, else null. The window shows it as an
    /// overlay; setting it back to null closes it.
    /// </summary>
    [ObservableProperty] private FindActionViewModel? _findAction;
}
