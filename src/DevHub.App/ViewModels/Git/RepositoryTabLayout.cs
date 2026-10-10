using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Core.Settings;

namespace DevHub.ViewModels.Git;

/// <summary>
/// Panel widths inside repository tabs, dragged with the splitters. One instance is shared by
/// all repository tabs (owned by <see cref="Documents.RepositoryDocumentFactory"/>), so
/// switching tabs doesn't make the panels jump. The widths aren't saved yet; the tree / flat choice is.
/// </summary>
public sealed partial class RepositoryTabLayout : ObservableObject
{
    private readonly ISettingsStore? _store;

    /// <param name="store">Where view preferences are saved (null in tests: not saved).</param>
    public RepositoryTabLayout(ISettingsStore? store = null)
    {
        _store = store;
        _changesAsTree = store?.Load().Git?.ChangesAsTree ?? true;
    }

    /// <summary>Local Changes shows folders (Fork's tree view) instead of a flat list. Saved.</summary>
    [ObservableProperty] private bool _changesAsTree;

    partial void OnChangesAsTreeChanged(bool value) =>
        _store?.Update(s => (s.Git ??= new GitSettings()).ChangesAsTree = value);

    /// <summary>The sidebar (header + Local Changes, All Commits, …).</summary>
    [ObservableProperty] private double _sidebarWidth = 240;

    /// <summary>The file lists in Local Changes (the diff pane takes the rest).</summary>
    [ObservableProperty] private double _changesListWidth = 320;

    /// <summary>All Commits: the details area under the commit list (message, files, diff).</summary>
    [ObservableProperty] private double _commitDetailsHeight = 340;

    /// <summary>All Commits: the message + file list column in the details (the diff takes the rest).</summary>
    [ObservableProperty] private double _commitFilesWidth = 320;
}
