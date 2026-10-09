using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Core.Git;
using DevHub.Services;

namespace DevHub.ViewModels.Git;

/// <summary>
/// The commit box under the file lists (Fork / JetBrains): message, Amend, Commit (Ctrl+Enter).
/// Kept per repository tab, so a half-written message survives switching tabs.
/// </summary>
public partial class CommitViewModel : ViewModelBase
{
    private readonly GitRepositoryClient? _client;
    private readonly IStatusService _status;
    private readonly Func<bool> _hasStagedChanges;
    private readonly Func<Task> _afterCommit;

    /// <summary>The message Amend filled in, to clear it again when Amend is turned off untouched.</summary>
    private string? _amendMessage;

    /// <param name="hasStagedChanges">Whether anything is staged (a commit needs something, unless amending).</param>
    /// <param name="afterCommit">Refreshes the repository after a commit.</param>
    public CommitViewModel(GitRepositoryClient? client, IStatusService status, Func<bool> hasStagedChanges,
        Func<Task> afterCommit)
    {
        _client = client;
        _status = status;
        _hasStagedChanges = hasStagedChanges;
        _afterCommit = afterCommit;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private string _message = "";

    /// <summary>Replace the last commit (its message is filled in to edit) instead of adding one.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private bool _isAmend;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private bool _isCommitting;

    /// <summary>Why the last commit failed (e.g. a pre-commit hook's output), or null.</summary>
    [ObservableProperty] private string? _error;

    /// <summary>Incremented to put the cursor in the message box (Ctrl+K); see Controls/FocusRequest.</summary>
    [ObservableProperty] private int _focusRequest;

    public string CommitButtonText => IsAmend ? "Amend Commit" : "Commit";

    /// <summary>Puts the cursor in the message box.</summary>
    public void RequestFocus() => FocusRequest++;

    /// <summary>Re-checks whether Commit is possible (the staged list changed).</summary>
    public void StagedChanged() => CommitCommand.NotifyCanExecuteChanged();

    private bool CanCommit() =>
        _client is not null && !IsCommitting && !string.IsNullOrWhiteSpace(Message)
        && (_hasStagedChanges() || IsAmend);

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync()
    {
        IsCommitting = true;
        Error = null;
        try
        {
            var id = await _client!.CommitAsync(Message, IsAmend);
            var subject = Message.Split('\n')[0].Trim();
            _status.Show($"{(IsAmend ? "Amended" : "Committed")} {id[..7]}: {subject}");
            _amendMessage = null;
            Message = "";
            IsAmend = false;
        }
        catch (Exception ex)
        {
            // Hooks, signing, "nothing to commit"…: git's own words, where the user is looking.
            Error = ex is GitException git && git.StandardError.Trim() is { Length: > 0 } stderr ? stderr : ex.Message;
            _status.Show("Commit failed.");
        }
        finally
        {
            IsCommitting = false;
        }
        await _afterCommit();
    }

    partial void OnIsAmendChanged(bool value)
    {
        OnPropertyChanged(nameof(CommitButtonText));
        if (value)
            _ = LoadLastMessageAsync();
        else if (_amendMessage is not null && Message == _amendMessage)
            Message = ""; // untouched: take the old message away again
    }

    // JetBrains fills in the last message when Amend is ticked, if the box is empty.
    private async Task LoadLastMessageAsync()
    {
        if (_client is null || !string.IsNullOrWhiteSpace(Message))
            return;

        var last = await _client.GetLastCommitMessageAsync();
        if (last is null)
        {
            _status.Show("Nothing to amend: there are no commits yet.");
            IsAmend = false;
            return;
        }
        if (IsAmend && string.IsNullOrWhiteSpace(Message))
            Message = _amendMessage = last;
    }
}
