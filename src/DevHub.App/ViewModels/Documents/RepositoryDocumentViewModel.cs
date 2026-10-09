using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Controls;
using DevHub.Core.Git;
using DevHub.Services;
using DevHub.ViewModels.Git;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// One repository open as an editor tab: Fork's per-repository workspace (a sidebar with
/// Local Changes, All Commits, Branches, Remotes, Tags, Stashes) in JetBrains style. Several can
/// be open side by side. Created by <see cref="RepositoryDocumentFactory"/>; disposed when its
/// tab closes.
/// </summary>
/// <remarks>
/// G1: status (branch, ahead/behind, changed files, merge/rebase in progress), refreshed when the
/// tab opens, when it becomes active, and whenever the repository changes on disk (a
/// <see cref="RepositoryWatcher"/>, so background tabs stay current too). Everything here only
/// reads the repository.
/// </remarks>
public sealed partial class RepositoryDocumentViewModel : DocumentViewModelBase, IDisposable
{
    public const string IdPrefix = "Repository:";

    private readonly GitRepositoryClient? _client;
    private readonly RepositoryWatcher? _watcher;
    private CancellationTokenSource? _refreshCts;
    private Task _refreshTask = Task.CompletedTask;
    private bool _disposed;

    /// <param name="path">Repository root folder.</param>
    /// <param name="client">Git access for this repository, or null when git isn't installed.</param>
    /// <param name="layout">Panel widths, shared with the other repository tabs.</param>
    /// <param name="editor">Opens files in the external editor for the repository's language.</param>
    public RepositoryDocumentViewModel(string path, GitRepositoryClient? client, RepositoryTabLayout layout,
        ExternalEditor editor)
    {
        Path = path;
        _client = client;
        Layout = layout;
        LocalChanges = new LocalChangesViewModel(layout, client,
            (file, line) => editor.Open(path, file.Change.Path, line), OpenDiffInTab);
        Name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : path;
        LocalChangesSection = Sections[0];
        _selectedSection = LocalChangesSection;

        if (_client is null)
            return;

        try
        {
            _watcher = new RepositoryWatcher(path);
            _watcher.Changed += OnRepositoryChanged;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Folder gone or unreadable: the refresh below reports it on the tab.
        }

        // One git status per open tab is cheap, and it gives every tab its dirty dot at startup.
        _refreshTask = RefreshAsync();
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

    /// <summary>"↑2 ↓1" against the upstream, or null when in sync / no upstream.</summary>
    [ObservableProperty] private string? _syncText;

    /// <summary>Upstream branch for the tooltip ("origin/main"), or null.</summary>
    [ObservableProperty] private string? _upstream;

    /// <summary>"Merge in progress" etc., or null. Shown as a banner (continue / abort arrive in G7).</summary>
    [ObservableProperty] private string? _operationText;

    /// <summary>Last refresh error (e.g. "not a git repository any more"), or null.</summary>
    [ObservableProperty] private string? _error;

    /// <summary>Changed files (the Local Changes section).</summary>
    public LocalChangesViewModel LocalChanges { get; }

    /// <summary>Panel widths (the splitters bind to these).</summary>
    public RepositoryTabLayout Layout { get; }

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

    private RepositorySection LocalChangesSection { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentContent))]
    private RepositorySection _selectedSection;

    /// <summary>
    /// What the content area shows: a section's ViewModel once it exists (Local Changes), else
    /// the section itself (the view has a placeholder template for <see cref="RepositorySection"/>).
    /// </summary>
    public object CurrentContent => SelectedSection == LocalChangesSection ? LocalChanges : SelectedSection;

    /// <summary>Re-reads the repository status. Called on open, activation, file changes and Git → Refresh.</summary>
    public Task RefreshAsync()
    {
        if (_client is null || _disposed)
            return Task.CompletedTask;

        // A newer refresh replaces an older one still running.
        // (Not disposed: the superseded refresh may still be using its token.)
        _refreshCts?.Cancel();
        var cts = _refreshCts = new CancellationTokenSource();
        return _refreshTask = RefreshCoreAsync(_client, cts.Token);
    }

    private async Task RefreshCoreAsync(GitRepositoryClient client, CancellationToken ct)
    {
        try
        {
            var status = await client.GetStatusAsync(ct);
            var operation = await client.GetOperationAsync(ct);

            Branch = status.DisplayBranch;
            Upstream = status.Upstream;
            SyncText = (status.Ahead, status.Behind) switch
            {
                (0, 0) => null,
                (var a, 0) => $"↑{a}",
                (0, var b) => $"↓{b}",
                var (a, b) => $"↑{a} ↓{b}",
            };
            OperationText = operation switch
            {
                RepositoryOperation.Merging => "Merge in progress",
                RepositoryOperation.Rebasing => "Rebase in progress",
                RepositoryOperation.CherryPicking => "Cherry-pick in progress",
                RepositoryOperation.Reverting => "Revert in progress",
                _ => null,
            };

            LocalChanges.Update(status);
            LocalChangesSection.Count = status.Changes.Count;
            IsDirty = status.Changes.Count > 0;
            Error = null;
        }
        catch (OperationCanceledException)
        {
            // Superseded or the tab was closed.
        }
        catch (Exception ex)
        {
            // Started fire-and-forget (constructor, activation, watcher): an exception escaping
            // here would vanish unseen, so show it on the tab instead (git failure, folder gone, …).
            Error = ex.Message;
        }
    }

    protected override void OnActivationChanged(bool isActive)
    {
        // Skip when a refresh is already running (e.g. the one started when the tab was created):
        // starting another would only cancel it and run git twice.
        if (isActive && _refreshTask.IsCompleted)
            _ = RefreshAsync();
    }

    /// <summary>"Open in New Tab" in the diff pane: the file's diff as its own tab, for more room.</summary>
    private void OpenDiffInTab(FileChangeViewModel file) =>
        OpenDocument?.Invoke(DiffDocumentFactory.IdFor(Path, file.Change.Path, file.IsStagedSide));

    // Raised on a background thread; ViewModel properties must change on the UI thread.
    private void OnRepositoryChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = RefreshAsync());

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_watcher is not null)
        {
            _watcher.Changed -= OnRepositoryChanged;
            _watcher.Dispose();
        }
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }
}

/// <summary>An entry in the repository sidebar, with an optional count (e.g. changed files).</summary>
public sealed partial class RepositorySection(string title, string iconKind, string placeholder) : ObservableObject
{
    public string Title { get; } = title;
    public string IconKind { get; } = iconKind;

    /// <summary>What the section will show and in which milestone (until it's built).</summary>
    public string Placeholder { get; } = placeholder;

    /// <summary>Shown as a badge when above zero.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    private int _count;

    public bool HasCount => Count > 0;

    public override string ToString() => HasCount ? $"{Title}, {Count}" : Title;
}
