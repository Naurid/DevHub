using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.ViewModels.Documents;

namespace DevHub.Actions.Providers;

/// <summary>
/// Git actions. They act on the **active repository tab** (<see cref="DocumentManager.ActiveRepository"/>)
/// and are disabled while the active tab isn't a repository. G0: Refresh; commit, push, branches…
/// join milestone by milestone (docs/GIT_CLIENT_PLAN.md).
/// </summary>
public sealed class GitActionProvider : IActionProvider, IDisposable
{
    private readonly DocumentManager _documents;
    private readonly AsyncRelayCommand _refresh;

    public GitActionProvider(DocumentManager documents)
    {
        _documents = documents;
        _refresh = new AsyncRelayCommand(
            () => _documents.ActiveRepository!.RefreshAsync(),
            () => _documents.ActiveRepository is not null);

        // Re-check "is a repository tab active?" when the active tab changes. Unsubscribed in Dispose.
        _documents.PropertyChanged += OnDocumentsChanged;
    }

    public IEnumerable<AppAction> GetActions()
    {
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
            _refresh.NotifyCanExecuteChanged();
    }

    public void Dispose() => _documents.PropertyChanged -= OnDocumentsChanged;
}
