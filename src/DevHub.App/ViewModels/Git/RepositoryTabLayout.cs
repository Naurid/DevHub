using CommunityToolkit.Mvvm.ComponentModel;

namespace DevHub.ViewModels.Git;

/// <summary>
/// Panel widths inside repository tabs, dragged with the splitters. One instance is shared by
/// all repository tabs (owned by <see cref="Documents.RepositoryDocumentFactory"/>), so
/// switching tabs doesn't make the panels jump. Not saved yet: back to the defaults on restart.
/// </summary>
public sealed partial class RepositoryTabLayout : ObservableObject
{
    /// <summary>The sidebar (header + Local Changes, All Commits, …).</summary>
    [ObservableProperty] private double _sidebarWidth = 240;

    /// <summary>The file lists in Local Changes (the diff pane takes the rest).</summary>
    [ObservableProperty] private double _changesListWidth = 300;

    /// <summary>All Commits: the details area under the commit list (message, files, diff).</summary>
    [ObservableProperty] private double _commitDetailsHeight = 340;

    /// <summary>All Commits: the message + file list column in the details (the diff takes the rest).</summary>
    [ObservableProperty] private double _commitFilesWidth = 320;
}
