using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Core.Git;

namespace DevHub.ViewModels.Git;

/// <summary>
/// The selected commit in All Commits: id, author, date, parents, full message, the files it
/// changed (compared with its first parent) and the selected file's diff.
/// </summary>
public partial class CommitDetailsViewModel : ViewModelBase
{
    private readonly GitRepositoryClient? _client;
    private CancellationTokenSource? _loadCts;

    public CommitDetailsViewModel(GitRepositoryClient? client, Action<FileChangeViewModel, int?>? openInEditor)
    {
        _client = client;
        Diff = new DiffViewModel(client, openInEditor);
    }

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
    public async Task LoadAsync(GitCommit? commit)
    {
        _loadCts?.Cancel();
        Commit = commit;
        Message = "";
        Files = [];
        Error = null;
        SelectedFile = null;

        if (commit is null || _client is null)
            return;

        var cts = _loadCts = new CancellationTokenSource();
        try
        {
            var message = await _client.GetCommitMessageAsync(commit.Id, cts.Token);
            var changes = await _client.GetCommitChangesAsync(commit, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            Message = message;
            Files = changes
                .OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                .Select(c => new FileChangeViewModel(c, staged: true))
                .ToList();
            SelectedFile = Files.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            // Another commit was selected meanwhile.
        }
        catch (Exception ex)
        {
            Error = "Couldn't load the commit: " + ex.Message;
        }
    }

    partial void OnSelectedFileChanged(FileChangeViewModel? value)
    {
        if (Commit is { } commit)
            _ = Diff.LoadCommitFileAsync(commit, value);
        else
            _ = Diff.LoadAsync(null);
    }

    private static string Short(string id) => id[..Math.Min(8, id.Length)];
}
