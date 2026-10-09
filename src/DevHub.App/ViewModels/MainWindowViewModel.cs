using DevHub.ViewModels.Documents;
using DevHub.ViewModels.Shell;
using DevHub.ViewModels.ToolWindows;

namespace DevHub.ViewModels;

/// <summary>
/// The shell's DataContext: the editor area (document tabs) in the center, the tool windows
/// docked around it, and the status bar. The title bar and menus get their behaviour from
/// actions, not from here.
/// </summary>
public class MainWindowViewModel(
    DocumentManager documents,
    ToolWindowManager toolWindows,
    StatusBarViewModel statusBar) : ViewModelBase
{
    /// <summary>Open tabs in the center, or the Welcome view when there are none.</summary>
    public DocumentManager Documents { get; } = documents;

    /// <summary>Tool windows docked around the editor area (stripes + slots).</summary>
    public ToolWindowManager ToolWindows { get; } = toolWindows;

    /// <summary>The status bar at the bottom of the window.</summary>
    public StatusBarViewModel StatusBar { get; } = statusBar;
}
