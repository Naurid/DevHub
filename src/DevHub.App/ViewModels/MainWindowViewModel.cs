using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.ViewModels.Pages;

namespace DevHub.ViewModels;

/// <summary>
/// The shell's DataContext: the list of navigable pages shown in the sidebar and the
/// one currently displayed in the content area.
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

    public MainWindowViewModel(
        RepositoriesViewModel repositories,
        TemplatesViewModel templates,
        GenerateViewModel generate,
        ImportViewModel import,
        SettingsViewModel settings,
        UiGalleryViewModel gallery)
    {
        Pages =
        [
            repositories,
            templates,
            generate,
            import,
            settings
        ];

#if DEBUG
        // Design-system playground; not shipped in Release builds.
        Pages.Add(gallery);
#endif

        _selectedPage = repositories;
    }
}
