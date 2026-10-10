using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Core.Git;

namespace DevHub.ViewModels.Git;

/// <summary>
/// The selected commit in All Commits: id, author, date, parents, full message, the files it
/// changed (compared with its first parent) and the selected file's diff. Also shows a stash
/// (Stashes section): its message, the files it holds (untracked ones too) and their diffs.
/// </summary>
public partial class CommitDetailsViewModel : ViewModelBase
{
    private readonly GitRepositoryClient? _client;
    private CancellationTokenSource? _loadCts;

    /// <summary>For a stash: which part (tracked changes, untracked files) each file's diff comes from.</summary>
    private Dictionary<FileChangeViewModel, GitCommit> _sources = [];

    /// <summary>Showing a stash rather than a commit (changes the texts).</summary>
    private readonly bool _isStash;

    /// <param name="annotate">Opens a blame tab for a file in the shown commit (path, commit id).</param>
    public CommitDetailsViewModel(RepositoryTabLayout layout, GitRepositoryClient? client,
        Action<FileChangeViewModel, int?>? openInEditor, Action<string, string?>? annotate = null, bool forStashes = false)
    {
        Layout = layout;
        _client = client;
        _isStash = forStashes;
        Diff = new DiffViewModel(client, openInEditor, annotate: annotate);
    }

    /// <summary>The file list's width (resizable), shared by all repository tabs.</summary>
    public RepositoryTabLayout Layout { get; }

    /// <summary>The empty state when nothing is selected.</summary>
    public string EmptyTitle => _isStash ? "No stash selected" : "No commit selected";
    public string EmptyText => _isStash
        ? "Select a stash to see its message, files and changes."
        : "Select a commit to see its message, files and changes.";

    /// <summary>The selected file's diff.</summary>
    public DiffViewModel Diff { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommit), nameof(AuthorText), nameof(DateText), nameof(IdText), nameof(ParentsText))]
    private GitCommit? _commit;

    public bool HasCommit => Commit is not null;

    public string AuthorText => Commit is null ? ""
        : Commit.CommitterName.Length > 0 && Commit.CommitterName != Commit.AuthorName
            ? $"{Commit.AuthorName} <{Commit.AuthorEmail}>, committed by {Commit.CommitterName}"
            : $"{Commit.AuthorName} <{Commit.AuthorEmail}>";

    public string DateText => Commit is null ? "" : $"{Commit.AuthorDate.ToLocalTime():yyyy-MM-dd HH:mm}";
    public string IdText => Commit?.Id ?? "";

    /// <summary>"Parents: 1a2b3c4d, 5e6f7a8b" for a merge, "Parent: …", or "Root commit".</summary>
    public string ParentsText => Commit switch
    {
        null => "",
        { ParentIds.Count: > 0 } c when _isStash => "Made on " + Short(c.ParentIds[0]) + (c.ParentIds.Count >= 3 ? " · includes untracked files" : ""),
        { ParentIds.Count: 0 } => "Root commit",
        { ParentIds.Count: 1 } c => "Parent: " + Short(c.ParentIds[0]),
        var c => "Merge of " + string.Join(", ", c.ParentIds.Select(Short)) + " (files: changes brought in, vs. the first parent)",
    };

    /// <summary>Full commit message; empty while loading.</summary>
    [ObservableProperty] private string _message = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilesText))]
    private IReadOnlyList<FileChangeViewModel> _files = [];

    /// <summary>"3 files changed".</summary>
    public string FilesText => Files.Count == 1 ? "1 file changed" : $"{Files.Count:N0} files changed";

    [ObservableProperty] private FileChangeViewModel? _selectedFile;

    [ObservableProperty] private string? _error;

    /// <summary>Shows <paramref name="commit"/> (null clears). The first file is selected so its diff shows at once.</summary>
    public Task LoadAsync(GitCommit? commit) =>
        LoadCoreAsync(commit, async (client, c, ct) =>
            (await client.GetCommitChangesAsync(c, ct)).Select(change => new CommitFile(change, c)).ToList());

    /// <summary>Shows a stash (null clears): its message and the files it holds, untracked ones included.</summary>
    public Task LoadStashAsync(GitStash? stash) =>
        LoadCoreAsync(stash?.Commit, (client, _, ct) => client.GetStashChangesAsync(stash!, ct));

    /// <param name="files">Lists the files and the commit each one's diff comes from.</param>
    private async Task LoadCoreAsync(GitCommit? commit,
        Func<GitRepositoryClient, GitCommit, CancellationToken, Task<IReadOnlyList<CommitFile>>> files)
    {
        _loadCts?.Cancel();
        Commit = commit;
        Message = "";
        Files = [];
        _sources = [];
        Error = null;
        SelectedFile = null;

        if (commit is null || _client is null)
            return;

        var cts = _loadCts = new CancellationTokenSource();
        try
        {
            var message = await _client.GetCommitMessageAsync(commit.Id, cts.Token);
            var changes = await files(_client, commit, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            Message = message;
            var rows = changes
                .OrderBy(f => f.Change.Path, StringComparer.OrdinalIgnoreCase)
                .Select(f => (Row: new FileChangeViewModel(f.Change, staged: true), f.Source))
                .ToList();
            _sources = rows.ToDictionary(r => r.Row, r => r.Source);
            Files = rows.Select(r => r.Row).ToList();
            SelectedFile = Files.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            // Another commit was selected meanwhile.
        }
        catch (Exception ex)
        {
            Error = (_isStash ? "Couldn't load the stash: " : "Couldn't load the commit: ") + ex.Message;
        }
    }

    partial void OnSelectedFileChanged(FileChangeViewModel? value)
    {
        if (Commit is { } commit)
            _ = Diff.LoadCommitFileAsync(value is not null && _sources.TryGetValue(value, out var source) ? source : commit, value);
        else
            _ = Diff.LoadAsync(null);
    }

    private static string Short(string id) => id[..Math.Min(8, id.Length)];
}
