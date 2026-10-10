using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.ViewModels.Documents;
using DevHub.ViewModels.Git;

namespace DevHub.Actions.Providers;

/// <summary>
/// Git actions. They act on the **active repository tab** (<see cref="DocumentManager.ActiveRepository"/>)
/// and are disabled while the active tab isn't a repository: commit, push / update / fetch, branches, refresh;
/// more join milestone by milestone (docs/GIT_CLIENT_PLAN.md).
/// </summary>
public sealed class GitActionProvider : IActionProvider, IDisposable
{
    private readonly DocumentManager _documents;
    private readonly AsyncRelayCommand _refresh;
    private readonly RelayCommand _commit;
    private readonly AsyncRelayCommand _discard;
    private readonly RelayCommand _branches;
    private readonly AsyncRelayCommand _newBranch;
    private readonly AsyncRelayCommand _fetch;
    private readonly AsyncRelayCommand _pull;
    private readonly AsyncRelayCommand _push;
    private readonly AsyncRelayCommand _forcePush;

    public GitActionProvider(DocumentManager documents)
    {
        _documents = documents;
        _refresh = new AsyncRelayCommand(
            () => _documents.ActiveRepository!.RefreshAsync(),
            () => _documents.ActiveRepository is not null);
        _commit = new RelayCommand(
            () => _documents.ActiveRepository!.FocusCommit(),
            () => _documents.ActiveRepository is not null);
        // Enabled whenever a repository tab is active (not only with a selection), so the key
        // always answers: it discards, or the status bar says what to select.
        _discard = new AsyncRelayCommand(
            () => _documents.ActiveRepository!.DiscardSelectionAsync(),
            () => _documents.ActiveRepository is not null);
        _branches = new RelayCommand(
            () => _documents.ActiveRepository!.BranchPopup.Open(),
            () => _documents.ActiveRepository is not null);
        _newBranch = new AsyncRelayCommand(
            () => _documents.ActiveRepository!.NewBranchAsync(),
            () => _documents.ActiveRepository is not null);

        // The active tab's own commands (the same ones its toolbar buttons use). While one is
        // already running there, they do nothing (their CanExecute is false).
        _fetch = RemoteCommand(r => r.FetchCommand);
        _pull = RemoteCommand(r => r.PullCommand);
        _push = RemoteCommand(r => r.PushCommand);
        _forcePush = RemoteCommand(r => r.ForcePushCommand);

        // Re-check "is a repository tab active?" when the active tab changes. Unsubscribed in Dispose.
        _documents.PropertyChanged += OnDocumentsChanged;
    }

    public IEnumerable<AppAction> GetActions()
    {
        // Ctrl+K = JetBrains' Commit: go to the commit box; Ctrl+Enter there commits.
        yield return new AppAction(ActionIds.GitCommit, "Commit…", _commit)
        {
            Category = "Git",
            IconKind = IconKind.Commit,
            Gesture = "Ctrl+K",
            Description = "Show Local Changes of the active repository with the cursor in the commit message.",
        };

        // Ctrl+Alt+Z = JetBrains' Rollback. Window-wide, so it works wherever the focus is.
        yield return new AppAction(ActionIds.GitDiscard, "Discard Changes…", _discard)
        {
            Category = "Git",
            IconKind = IconKind.Delete,
            Gesture = "Ctrl+Alt+Z",
            Description = "Throw away the unstaged changes of the file selected in Local Changes (asks first).",
        };

        // Ctrl+Shift+` = JetBrains' Branches popup (the key left of 1 on US keyboards).
        yield return new AppAction(ActionIds.GitBranches, "Branches…", _branches)
        {
            Category = "Git",
            IconKind = IconKind.Vcs,
            Gesture = "Ctrl+Shift+OemTilde",
            Description = "Search the active repository's branches and check one out.",
        };

        // JetBrains: Push… = Ctrl+Shift+K, Update Project… = Ctrl+T.
        yield return new AppAction(ActionIds.GitPush, "Push…", _push)
        {
            Category = "Git",
            IconKind = IconKind.Push,
            Gesture = "Ctrl+Shift+K",
            Description = "Push the current branch of the active repository (shows the commits first).",
        };

        yield return new AppAction(ActionIds.GitPull, "Update", _pull)
        {
            Category = "Git",
            IconKind = IconKind.Update,
            Gesture = "Ctrl+T",
            Description = "Pull the current branch's new commits (fast-forward; offers a rebase if both sides changed).",
        };

        yield return new AppAction(ActionIds.GitFetch, "Fetch", _fetch)
        {
            Category = "Git",
            IconKind = IconKind.Fetch,
            Description = "Download what's new on every remote of the active repository; changes nothing locally.",
        };

        yield return new AppAction(ActionIds.GitForcePush, "Force Push…", _forcePush)
        {
            Category = "Git",
            IconKind = IconKind.Push,
            Description = "Overwrite the remote branch with yours (after amend / rebase). Asks first; --force-with-lease.",
        };

        yield return new AppAction(ActionIds.GitNewBranch, "New Branch…", _newBranch)
        {
            Category = "Git",
            IconKind = IconKind.Add,
            Description = "Create a branch from the current commit in the active repository.",
        };

        // Ctrl+Alt+Y = JetBrains' "Synchronize" (F5 is already Rescan Repositories).
        yield return new AppAction(ActionIds.GitRefresh, "Refresh Repository", _refresh)
        {
            Category = "Git",
            IconKind = IconKind.Refresh,
            Gesture = "Ctrl+Alt+Y",
            Description = "Re-read the active repository tab (branch, and later changes).",
        };
    }

    private AsyncRelayCommand RemoteCommand(Func<RemoteOperations, IAsyncRelayCommand> pick) =>
        new(async () =>
            {
                if (_documents.ActiveRepository?.Remote is { } remote && pick(remote) is { } command && command.CanExecute(null))
                    await command.ExecuteAsync(null);
            },
            () => _documents.ActiveRepository is not null);

    private void OnDocumentsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentManager.ActiveRepository))
        {
            _refresh.NotifyCanExecuteChanged();
            _commit.NotifyCanExecuteChanged();
            _discard.NotifyCanExecuteChanged();
            _branches.NotifyCanExecuteChanged();
            _newBranch.NotifyCanExecuteChanged();
            _fetch.NotifyCanExecuteChanged();
            _pull.NotifyCanExecuteChanged();
            _push.NotifyCanExecuteChanged();
            _forcePush.NotifyCanExecuteChanged();
        }
    }

    public void Dispose() => _documents.PropertyChanged -= OnDocumentsChanged;
}
