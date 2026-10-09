using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Controls;
using DevHub.Core.Git;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// One repository open as an editor tab: Fork's per-repository workspace (a sidebar with
/// Local Changes, All Commits, Branches, Remotes, Tags, Stashes) in JetBrains style. Several can
/// be open side by side. Created by <see cref="RepositoryDocumentFactory"/>; disposed when its
/// tab closes.
/// </summary>
/// <remarks>
/// G0: header (name, path, branch) and the sidebar; the sections fill in milestone by milestone
/// (docs/GIT_CLIENT_PLAN.md). Everything here only reads the repository.
/// </remarks>
public sealed partial class RepositoryDocumentViewModel : DocumentViewModelBase, IDisposable
{
    public const string IdPrefix = "Repository:";

    private readonly GitRepositoryClient? _client;
    private CancellationTokenSource? _refreshCts;

    /// <param name="path">Repository root folder.</param>
    /// <param name="client">Git access for this repository, or null when git isn't installed.</param>
    public RepositoryDocumentViewModel(string path, GitRepositoryClient? client)
    {
        Path = path;
        _client = client;
        Name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : path;
        _selectedSection = Sections[0];
        // No refresh here: it runs when the tab becomes active (OnActivationChanged), so tabs
        // restored in the background don't all run git at startup.
    }

    public override string Id => IdPrefix + Path;
    public override string Title => Name;
    public override string Icon => IconKind.Vcs;

    /// <summary>The sidebar runs edge to edge (Fork-style), so no editor-area padding.</summary>
    public override bool FillsEditorArea => true;

    /// <summary>Repository root folder.</summary>
    public string Path { get; }

    /// <summary>Folder name, shown as the tab title.</summary>
    public string Name { get; }

    /// <summary>False when git isn't installed; the view then shows how to fix that.</summary>
    public bool IsGitAvailable => _client is not null;

    /// <summary>Checked-out branch, or the short commit id when HEAD is detached; null until loaded.</summary>
    [ObservableProperty] private string? _branch;

    /// <summary>Last refresh error (e.g. "not a git repository any more"), or null.</summary>
    [ObservableProperty] private string? _error;

    /// <summary>The sidebar (Fork-style). Each section says which milestone fills it in.</summary>
    public IReadOnlyList<RepositorySection> Sections { get; } =
    [
        new("Local Changes", IconKind.Edit, "Changed files, staging and the commit box arrive in G1–G3."),
        new("All Commits", IconKind.Commit, "The commit graph arrives in G5."),
        new("Branches", IconKind.Vcs, "Local branches, checkout and branch management arrive in G4."),
        new("Remotes", IconKind.Fetch, "Remotes and their branches arrive in G4, fetch / pull / push in G6."),
        new("Tags", IconKind.Template, "Tags arrive in G8."),
        new("Stashes", IconKind.Copy, "Stashes arrive in G8."),
    ];

    [ObservableProperty] private RepositorySection _selectedSection;

    /// <summary>Re-reads the repository (G0: the branch). Called whenever the tab is activated, and by Git → Refresh.</summary>
    public async Task RefreshAsync()
    {
        if (_client is null)
            return;

        // A newer refresh replaces an older one still running.
        _refreshCts?.Cancel();
        var cts = _refreshCts = new CancellationTokenSource();
        try
        {
            Branch = await _client.GetBranchAsync(cts.Token);
            Error = null;
        }
        catch (OperationCanceledException)
        {
            // Superseded or the tab was closed.
        }
        catch (Exception ex)
        {
            // Started fire-and-forget (constructor, tab activation): an exception escaping here
            // would vanish unseen, so show it on the tab instead (git failure, folder gone, …).
            Error = ex.Message;
        }
    }

    protected override void OnActivationChanged(bool isActive)
    {
        if (isActive)
            _ = RefreshAsync();
    }

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }
}

/// <summary>An entry in the repository sidebar.</summary>
public sealed record RepositorySection(string Title, string IconKind, string Placeholder)
{
    public override string ToString() => Title;
}
