using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.ViewModels.Documents;
using DevHub.ViewModels.Shell;
using DevHub.ViewModels.ToolWindows;

namespace DevHub.ViewModels;

/// <summary>
/// The shell's DataContext: the editor area (document tabs) in the center, the tool windows
/// docked around it, the status bar, and the Find Action overlay while it's open. The title
/// bar and menus get their behaviour from actions, not from here.
/// </summary>
public partial class MainWindowViewModel(
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

    /// <summary>
    /// The Find Action popup (Ctrl+Shift+A) while open, else null. The window shows it as an
    /// overlay; setting it back to null closes it.
    /// </summary>
    [ObservableProperty] private FindActionViewModel? _findAction;
}
