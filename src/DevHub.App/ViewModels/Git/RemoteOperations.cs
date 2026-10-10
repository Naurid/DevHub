using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Core.Git;
using DevHub.Services;
using DevHub.ViewModels.Dialogs;

namespace DevHub.ViewModels.Git;

/// <summary>
/// Fetch, Update (pull) and Push for one repository, so the tab's toolbar, the commit box's
/// "Commit and Push" and the Git actions (Ctrl+T, Ctrl+Shift+K) behave the same. Each runs in
/// the status bar with progress and Cancel, explains failures in plain words, offers the
/// usual way out (diverged → rebase, rejected → update first), and refreshes the repository.
/// One at a time per repository (<see cref="IsBusy"/>).
/// </summary>
public sealed partial class RemoteOperations : ObservableObject
{
    private readonly GitRepositoryClient? _client;
    private readonly IDialogService _dialogs;
    private readonly IStatusService _status;
    private readonly Func<Task> _refresh;
    private readonly Func<IReadOnlyList<GitBranch>> _branches;
    private readonly string _repositoryName;

    /// <param name="branches">The branches as last loaded (for the current branch, its upstream and ahead/behind).</param>
    public RemoteOperations(GitRepositoryClient? client, IDialogService dialogs, IStatusService status,
        Func<Task> refresh, Func<IReadOnlyList<GitBranch>> branches, string repositoryName)
    {
        _client = client;
        _dialogs = dialogs;
        _status = status;
        _refresh = refresh;
        _branches = branches;
        _repositoryName = repositoryName;
    }

    /// <summary>A fetch / pull / push is running in this repository.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand), nameof(PullCommand), nameof(PushCommand), nameof(ForcePushCommand))]
    private bool _isBusy;

    private bool CanRun() => _client is not null && !IsBusy;

    private GitBranch? Current => _branches().FirstOrDefault(b => b.IsCurrent && !b.IsRemote);

    // ---- Fetch ----------------------------------------------------------------------------------

    /// <summary>Downloads what's new on every remote; changes nothing locally.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task FetchAsync()
    {
        if (!await RunAsync($"Fetching {_repositoryName}", (p, ct) => _client!.FetchAsync(p, ct)))
            return;

        // Read the status here rather than using the refreshed branch list: the fetch also
        // wakes the file watcher, whose refresh can replace the one RunAsync waited for.
        var s = await _client!.GetStatusAsync();
        _status.Show(s switch
        {
            { Upstream: null } => "Fetched.",
            { Behind: > 0 } => $"Fetched: {s.DisplayBranch} is {s.Behind} commit{S(s.Behind)} behind {s.Upstream}. Update (Ctrl+T) to bring them in.",
            _ => $"Fetched: {s.DisplayBranch} is up to date with {s.Upstream}.",
        });
    }

    // ---- Update (pull) --------------------------------------------------------------------------

    /// <summary>
    /// Brings the current branch up to date: fast-forward only; if both sides have new commits,
    /// offers to rebase the local ones on top (JetBrains' Update Project, Ctrl+T).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task PullAsync()
    {
        if (Current is not { } branch)
        {
            _status.Show("Not on a branch (detached HEAD): nothing to update.");
            return;
        }
        if (branch.Upstream is null)
        {
            await ShowMessageAsync("No Upstream Branch",
                $"{branch.Name} isn't connected to a remote branch, so there's nothing to update from. Push it once to create one.");
            return;
        }

        try
        {
            if (await RunAsync($"Updating {branch.Name}", (p, ct) => _client!.PullAsync(PullMode.FastForwardOnly, p, ct), throwOn: RemoteErrorKind.Diverged))
                _status.Show($"{branch.Name} is up to date with {branch.Upstream}.");
        }
        catch (RemoteOperationException)
        {
            // Diverged: both sides have commits. Rebasing replays ours on top; nothing is lost.
            var rebase = new ConfirmDialogViewModel("Branches Have Diverged",
                $"{branch.Name} and {branch.Upstream} both have new commits. Rebase your local commits on top of " +
                $"{branch.Upstream}? Uncommitted changes are put aside and restored afterwards.",
                "Rebase", destructive: false);
            if (!await _dialogs.ShowDialogAsync(rebase))
            {
                _status.Show("Update cancelled; nothing changed.");
                return;
            }
            if (await RunAsync($"Rebasing {branch.Name}", (p, ct) => _client!.PullAsync(PullMode.Rebase, p, ct)))
                _status.Show($"Rebased {branch.Name} onto {branch.Upstream}.");
        }
    }

    // ---- Push -----------------------------------------------------------------------------------

    /// <summary>
    /// Pushes the current branch to its upstream after showing what will go (JetBrains' Push,
    /// Ctrl+Shift+K). A branch without an upstream is published to "origin" (or the only remote).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task PushAsync() => PushCoreAsync(force: false, confirm: true);

    /// <summary>Pushes without the confirmation (after Commit and Push, where the user just asked for it).</summary>
    public Task PushWithoutAskingAsync() => PushCoreAsync(force: false, confirm: false);

    /// <summary>
    /// Overwrites the remote branch with the local one (after amending or rebasing pushed
    /// commits). Always asks; uses --force-with-lease, so it fails instead of deleting commits
    /// someone pushed since the last fetch.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ForcePushAsync() => PushCoreAsync(force: true, confirm: true);

    private async Task PushCoreAsync(bool force, bool confirm)
    {
        if (_client is null)
            return;
        if (Current is not { } branch)
        {
            _status.Show("Not on a branch (detached HEAD): nothing to push.");
            return;
        }

        string? newUpstreamRemote = null;
        if (branch.Upstream is null)
        {
            var remotes = await _client.GetRemotesAsync();
            newUpstreamRemote = remotes.Contains("origin") ? "origin" : remotes.FirstOrDefault();
            if (newUpstreamRemote is null)
            {
                await ShowMessageAsync("No Remote",
                    "This repository has no remote to push to. Add one first (for example \"git remote add origin <url>\").");
                return;
            }
            if (confirm && !await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel("Push New Branch",
                    $"Push {branch.Name} to {newUpstreamRemote}? It's created there as {newUpstreamRemote}/{branch.Name}, " +
                    "and this branch tracks it from now on.", "Push", destructive: false)))
                return;
        }
        else if (force)
        {
            var confirmForce = new ConfirmDialogViewModel("Force Push",
                $"Overwrite {branch.Upstream} with your {branch.Name}? Commits on {branch.Upstream} that aren't in your " +
                $"branch will be removed from it. (Safe mode: if someone pushed since your last fetch, nothing is overwritten.)",
                "Force Push", destructive: true);
            if (!await _dialogs.ShowDialogAsync(confirmForce))
                return;
        }
        else
        {
            var outgoing = await _client.GetUnpushedCommitsAsync();
            if (outgoing.Count == 0)
            {
                _status.Show($"Nothing to push: {branch.Name} has no commits that {branch.Upstream} doesn't have.");
                return;
            }
            if (confirm && !await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel("Push",
                    $"Push {outgoing.Count} commit{S(outgoing.Count)} from {branch.Name} to {branch.Upstream}?", "Push",
                    outgoing.Select(c => $"{c.ShortId[..7]}  {c.Subject}").ToList(), destructive: false)))
                return;
        }

        var target = newUpstreamRemote is null ? branch.Upstream : $"{newUpstreamRemote}/{branch.Name}";
        try
        {
            if (await RunAsync($"Pushing {branch.Name}",
                    (p, ct) => _client.PushAsync(newUpstreamRemote, force, p, ct), throwOn: RemoteErrorKind.Rejected))
                _status.Show($"Pushed {branch.Name} to {target}.");
        }
        catch (RemoteOperationException)
        {
            // Rejected: the remote has commits we don't. Update first (rebase), then push again.
            var update = new ConfirmDialogViewModel("Push Rejected",
                $"{target} has commits that you don't have yet. Rebase your commits on top of them and push again? " +
                "(If you amended or rebased commits that were already pushed, use Git → Force Push instead.)",
                "Rebase and Push", destructive: false);
            if (!await _dialogs.ShowDialogAsync(update))
            {
                _status.Show("Push cancelled; nothing was sent.");
                return;
            }
            if (await RunAsync($"Rebasing {branch.Name}", (p, ct) => _client.PullAsync(PullMode.Rebase, p, ct))
                && await RunAsync($"Pushing {branch.Name}", (p, ct) => _client.PushAsync(null, false, p, ct)))
                _status.Show($"Rebased onto {target} and pushed.");
        }
    }

    // ---- Running ---------------------------------------------------------------------------------

    /// <summary>
    /// Runs another network command (pushing a tag, updating submodules) like Fetch / Push: status
    /// bar progress with Cancel, failures explained, then a refresh. False when it failed, was
    /// cancelled, or another one is still running.
    /// </summary>
    public async Task<bool> RunNetworkAsync(string title, Func<IProgress<GitProgress>, CancellationToken, Task> operation)
    {
        if (!CanRun())
        {
            _status.Show(IsBusy ? "Another fetch / push is still running; try again when it's done." : "Git is not available.");
            return false;
        }
        return await RunAsync(title, operation);
    }

    /// <summary>
    /// Runs one network operation in the status bar (progress, Cancel), then refreshes the
    /// repository. Returns true when it succeeded; on failure explains it in a dialog and returns
    /// false, except for <paramref name="throwOn"/>, which is rethrown for the caller to handle.
    /// </summary>
    private async Task<bool> RunAsync(string title, Func<IProgress<GitProgress>, CancellationToken, Task> operation,
        RemoteErrorKind? throwOn = null)
    {
        using var cts = new CancellationTokenSource();
        IsBusy = true;
        var task = _status.StartTask(title + "…", cts.Cancel);
        try
        {
            await operation(new StatusProgress(task), cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            _status.Show($"{title}: cancelled.");
            return false;
        }
        catch (RemoteOperationException ex) when (ex.Error.Kind == throwOn)
        {
            throw;
        }
        catch (RemoteOperationException ex)
        {
            _status.Show($"{title}: {ex.Error.Title}.");
            var gitOutput = ex.GitOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => ProgressParser.Parse(l) is null) // progress noise isn't useful here
                .ToList();
            await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel(ex.Error.Title, ex.Error.Explanation, "OK",
                gitOutput, destructive: false, showCancel: false));
            return false;
        }
        catch (Exception ex)
        {
            _status.Show($"{title}: {ex.Message}");
            return false;
        }
        finally
        {
            task.Dispose();
            IsBusy = false;
            await _refresh();
        }
    }

    private Task ShowMessageAsync(string title, string message) =>
        _dialogs.ShowDialogAsync(new ConfirmDialogViewModel(title, message, "OK", destructive: false, showCancel: false));

    private static string S(int n) => n == 1 ? "" : "s";

    /// <summary>git's progress → the status bar (called on git's reader thread; the task marshals to the UI).</summary>
    private sealed class StatusProgress(IStatusTask task) : IProgress<GitProgress>
    {
        public void Report(GitProgress value) => task.Report(value.Phase, value.Percent);
    }
}
