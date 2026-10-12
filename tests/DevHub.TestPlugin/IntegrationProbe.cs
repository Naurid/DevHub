using System.Text.RegularExpressions;
using DevHub.Integration;
using DevHub.Services;

namespace DevHub.Plugins;

/// <summary>
/// The test plugin's side of the Git hooks (DevHub.Integration), to see them work end to end with the Git plugin:
/// suggests "DEV-7: " on a branch like feature/DEV-7-…, marks every commit and branch with a success badge, and says
/// in the status bar what the repository events reported. Only what a real plugin (Board.Git, CI) would do, simplified.
/// </summary>
public sealed partial class IntegrationProbe : ICommitMessageSuggester, IGitDecorator, IDisposable
{
    private readonly IStatusService _status;
    private readonly IRepositoryEvents? _events;

    public IntegrationProbe(IStatusService status, IRepositoryEvents? events = null)
    {
        _status = status;
        _events = events;
        if (_events is null)
            return;
        _events.BranchChanged += OnBranchChanged;
        _events.Committed += OnCommitted;
        _events.Pushed += OnPushed;
        _events.Fetched += OnFetched;
    }

    // ---- ICommitMessageSuggester ----

    [GeneratedRegex(@"(?<![A-Za-z])([A-Z][A-Z0-9]+-\d+)")]
    private static partial Regex IssueKey();

    public Task<string?> SuggestAsync(CommitContext context, CancellationToken ct) =>
        Task.FromResult(context.Branch is { } branch && IssueKey().Match(branch) is { Success: true } key ? key.Value + ": " : null);

    // ---- IGitDecorator ----

    private static readonly IReadOnlyList<Decoration> Mark = [new("info", DecorationTone.Success, "Test plugin was here")];

    public IReadOnlyDictionary<string, IReadOnlyList<Decoration>> DecorateCommits(string repositoryFolder, IReadOnlyList<string> commitShas) =>
        commitShas.ToDictionary(sha => sha, _ => Mark);

    public IReadOnlyDictionary<string, IReadOnlyList<Decoration>> DecorateBranches(string repositoryFolder, IReadOnlyList<string> branches) =>
        branches.ToDictionary(branch => branch, _ => Mark);

    public event EventHandler<DecorationsChangedEventArgs>? Changed { add { } remove { } } // its marks never change

    // ---- IRepositoryEvents ----

    private void OnBranchChanged(object? sender, RepositoryEventArgs e) => _status.Show($"Test plugin heard: branch changed to {e.Branch}");
    private void OnCommitted(object? sender, RepositoryEventArgs e) => _status.Show($"Test plugin heard: committed {e.CommitSha?[..7]} on {e.Branch}");
    private void OnPushed(object? sender, PushEventArgs e) => _status.Show($"Test plugin heard: pushed {e.Branch} to {e.Remote}/{e.RemoteBranch}");
    private void OnFetched(object? sender, RepositoryEventArgs e) => _status.Show($"Test plugin heard: fetched into {e.Branch}");

    public void Dispose()
    {
        if (_events is null)
            return;
        _events.BranchChanged -= OnBranchChanged;
        _events.Committed -= OnCommitted;
        _events.Pushed -= OnPushed;
        _events.Fetched -= OnFetched;
    }
}
