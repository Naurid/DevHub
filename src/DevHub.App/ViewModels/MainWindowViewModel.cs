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
public partial class MainWindowViewModel : ViewModelBase
{
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
