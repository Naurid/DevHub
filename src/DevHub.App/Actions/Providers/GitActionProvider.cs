using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.ViewModels.Documents;

namespace DevHub.Actions.Providers;

/// <summary>
/// Git actions. They act on the **active repository tab** (<see cref="DocumentManager.ActiveRepository"/>)
/// and are disabled while the active tab isn't a repository. Commit (Ctrl+K) and Refresh so far; push, branches…
/// join milestone by milestone (docs/GIT_CLIENT_PLAN.md).
/// </summary>
public sealed class GitActionProvider : IActionProvider, IDisposable
{
    private readonly DocumentManager _documents;
    private readonly AsyncRelayCommand _refresh;
    private readonly RelayCommand _commit;
    private readonly AsyncRelayCommand _discard;
    private readonly RelayCommand _branches;
    private readonly AsyncRelayCommand _newBranch;

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

    private void OnDocumentsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentManager.ActiveRepository))
        {
            _refresh.NotifyCanExecuteChanged();
            _commit.NotifyCanExecuteChanged();
            _discard.NotifyCanExecuteChanged();
            _branches.NotifyCanExecuteChanged();
            _newBranch.NotifyCanExecuteChanged();
        }
    }

    public void Dispose() => _documents.PropertyChanged -= OnDocumentsChanged;
}
