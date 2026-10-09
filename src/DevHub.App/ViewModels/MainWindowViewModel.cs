using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.ViewModels.Pages;
using DevHub.ViewModels.Shell;
using DevHub.ViewModels.ToolWindows;

namespace DevHub.ViewModels;

/// <summary>
/// The shell's DataContext: the list of navigable pages shown in the sidebar, the
/// one currently displayed in the content area, the status bar and the tool windows.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>Pages shown in the sidebar, in order.</summary>
    public ObservableCollection<PageViewModelBase> Pages { get; }

    /// <summary>
    /// The page the content area renders. Bound two-way to the sidebar's selection;
    /// [ObservableProperty] generates the property + change notification from this field.
    /// </summary>
    [ObservableProperty]
    private PageViewModelBase _selectedPage;

    /// <summary>The status bar at the bottom of the window.</summary>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>Tool windows docked around the content area (stripes + slots).</summary>
    public ToolWindowManager ToolWindows { get; }

    public MainWindowViewModel(
        TemplatesViewModel templates,
        GenerateViewModel generate,
        ImportViewModel import,
        SettingsViewModel settings,
        UiGalleryViewModel gallery,
        StatusBarViewModel statusBar,
        ToolWindowManager toolWindows)
    {
        StatusBar = statusBar;
        ToolWindows = toolWindows;

        Pages =
        [
            templates,
            generate,
            import,
            settings
        ];

#if DEBUG
        // Design-system playground; not shipped in Release builds.
        Pages.Add(gallery);
#endif

        // Repositories moved to a tool window (UI-0 step 7); pages go away in step 8.
        _selectedPage = templates;
    }
}
