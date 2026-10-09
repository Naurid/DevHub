using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Core.Git;
using DevHub.Services;
using DevHub.ViewModels.Dialogs;

namespace DevHub.ViewModels.Git;

/// <summary>
/// Fork's Local Changes view for one repository tab: what changed, split into Unstaged (incl.
/// untracked and conflicts) and Staged, the selected file's diff on the right, and the commit
/// box. Stage / unstage with double-click, Space or the header buttons; discard asks first.
/// </summary>
public partial class LocalChangesViewModel : ViewModelBase
{
    private readonly GitRepositoryClient? _client;
    private readonly IDialogService _dialogs;
    private readonly IStatusService _status;
    private readonly Func<Task> _refresh;

    /// <summary>True while <see cref="Update"/> refills the lists (selection changes then are temporary).</summary>
    private bool _updating;

    /// <param name="layout">Panel widths, shared by all repository tabs.</param>
    /// <param name="client">Git access (null when git isn't installed: everything is disabled).</param>
    /// <param name="dialogs">For the discard confirmation.</param>
    /// <param name="status">Status bar: results and failures.</param>
    /// <param name="refresh">Re-reads the repository status after an operation.</param>
    /// <param name="openInEditor">Opens a file in the external editor for the repository's language, at a line.</param>
    /// <param name="openInTab">Opens a file's diff as its own tab.</param>
    public LocalChangesViewModel(RepositoryTabLayout layout, GitRepositoryClient? client,
        IDialogService dialogs, IStatusService status, Func<Task> refresh,
        Action<FileChangeViewModel, int?>? openInEditor, Action<FileChangeViewModel>? openInTab)
    {
        Layout = layout;
        _client = client;
        _dialogs = dialogs;
        _status = status;
        _refresh = refresh;
        Diff = new DiffViewModel(client, openInEditor, openInTab);
        Commit = new CommitViewModel(client, status, () => Staged.Count > 0, refresh);
    }

    /// <summary>The commit box (message, Amend, Commit).</summary>
    public CommitViewModel Commit { get; }

    /// <summary>A stage / unstage / discard is running: the buttons wait for it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StageSelectedCommand), nameof(UnstageSelectedCommand),
        nameof(StageAllCommand), nameof(UnstageAllCommand), nameof(DiscardSelectedCommand), nameof(DiscardAllCommand))]
    private bool _isBusy;

    public ObservableCollection<FileChangeViewModel> Unstaged { get; } = new();
    public ObservableCollection<FileChangeViewModel> Staged { get; } = new();

    public int UnstagedCount => Unstaged.Count;
    public int StagedCount => Staged.Count;

    /// <summary>The diff of the selected file (right-hand pane).</summary>
    public DiffViewModel Diff { get; }

    public bool HasChanges => Unstaged.Count + Staged.Count > 0;

    /// <summary>Panel widths (shared by all repository tabs); the file-list splitter binds to it.</summary>
    public RepositoryTabLayout Layout { get; }

    // One selection across both lists: picking in one clears the other.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StageSelectedCommand), nameof(DiscardSelectedCommand))]
    private FileChangeViewModel? _selectedUnstaged;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnstageSelectedCommand))]
    private FileChangeViewModel? _selectedStaged;

    /// <summary>The selected file in either list; the diff pane shows it.</summary>
    public FileChangeViewModel? Selected => SelectedUnstaged ?? SelectedStaged;

    /// <summary>
    /// Replaces the lists with a new status, keeping the selection when the file is still there,
    /// and reloads its diff (the file may have changed even if its status didn't).
    /// </summary>
    public void Update(RepoStatus status)
    {
        _updating = true;
        try
        {
            Refill(status);
        }
        finally
        {
            _updating = false;
        }

        // The rows are new objects, so this reloads the diff; the old lines stay on screen
        // until the new ones arrive (no flicker).
        if (Selected != Diff.File)
            _ = Diff.LoadAsync(Selected);
    }

    private void Refill(RepoStatus status)
    {
        var keepUnstaged = SelectedUnstaged?.Change.Path;
        var keepStaged = SelectedStaged?.Change.Path;

        Fill(Unstaged, status.Changes
            .Where(c => c.IsUnstaged || c.IsConflicted)
            // Conflicts first (they block everything), then by path.
            .OrderBy(c => c.IsConflicted ? 0 : 1)
            .ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .Select(c => new FileChangeViewModel(c, staged: false, ToggleStage)));
        Fill(Staged, status.Changes
            .Where(c => c.IsStaged)
            .OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .Select(c => new FileChangeViewModel(c, staged: true, ToggleStage)));

        SelectedUnstaged = Unstaged.FirstOrDefault(f => f.Change.Path == keepUnstaged);
        SelectedStaged = Staged.FirstOrDefault(f => f.Change.Path == keepStaged);

        OnPropertyChanged(nameof(UnstagedCount));
        OnPropertyChanged(nameof(StagedCount));
        OnPropertyChanged(nameof(HasChanges));
        StageAllCommand.NotifyCanExecuteChanged();
        UnstageAllCommand.NotifyCanExecuteChanged();
        DiscardAllCommand.NotifyCanExecuteChanged();
        Commit.StagedChanged();
    }

    // ---- Stage / unstage / discard ---------------------------------------------------------

    /// <summary>Double-click on a row: stage it if unstaged, unstage it if staged.</summary>
    private void ToggleStage(FileChangeViewModel file)
    {
        if (file.IsStagedSide)
            _ = UnstageAsync([file]);
        else
            _ = StageAsync([file]);
    }

    private bool CanStageSelected() => !IsBusy && SelectedUnstaged is not null;

    /// <summary>Space in the Unstaged list, or "Stage".</summary>
    [RelayCommand(CanExecute = nameof(CanStageSelected))]
    private Task StageSelectedAsync() => StageAsync([SelectedUnstaged!]);

    private bool CanUnstageSelected() => !IsBusy && SelectedStaged is not null;

    /// <summary>Space in the Staged list, or "Unstage".</summary>
    [RelayCommand(CanExecute = nameof(CanUnstageSelected))]
    private Task UnstageSelectedAsync() => UnstageAsync([SelectedStaged!]);

    private bool CanStageAll() => !IsBusy && _client is not null && Unstaged.Any(f => !f.IsConflicted);

    [RelayCommand(CanExecute = nameof(CanStageAll))]
    private Task StageAllAsync() => StageAsync(Unstaged.ToList());

    private bool CanUnstageAll() => !IsBusy && _client is not null && Staged.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUnstageAll))]
    private Task UnstageAllAsync() => RunAsync("Unstage all", client => client.UnstageAllAsync(), Staged, 0);

    private bool CanDiscardSelected() => !IsBusy && SelectedUnstaged is { IsConflicted: false };

    /// <summary>Throws away the selected file's unstaged changes, after asking.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscardSelected))]
    private Task DiscardSelectedAsync() => DiscardAsync([SelectedUnstaged!]);

    /// <summary>
    /// Git → Discard Changes… (Ctrl+Alt+Z, window-wide): discards the selected unstaged file,
    /// or says in the status bar why it can't, instead of silently doing nothing.
    /// </summary>
    public Task DiscardSelectionAsync()
    {
        if (IsBusy)
            return Task.CompletedTask;
        if (SelectedUnstaged is null)
        {
            _status.Show(SelectedStaged is not null
                ? "Discard works on unstaged changes. Unstage the file first (Space), then discard it."
                : "Select a file in Unstaged to discard its changes.");
            return Task.CompletedTask;
        }
        if (SelectedUnstaged.IsConflicted)
        {
            _status.Show("Conflicted files can't be discarded here (conflict resolution arrives in G7).");
            return Task.CompletedTask;
        }
        return DiscardAsync([SelectedUnstaged]);
    }

    private bool CanDiscardAll() => !IsBusy && _client is not null && Unstaged.Any(f => !f.IsConflicted);

    /// <summary>Throws away all unstaged changes (staged ones are kept), after asking.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscardAll))]
    private Task DiscardAllAsync() => DiscardAsync(Unstaged.Where(f => !f.IsConflicted).ToList());

    private Task StageAsync(IReadOnlyList<FileChangeViewModel> files)
    {
        // "git add" on a conflicted file marks it resolved, conflict markers and all: that's G7's job.
        if (files.Any(f => f.IsConflicted))
        {
            _status.Show("Resolve the conflict before staging (conflict resolution arrives in G7).");
            files = files.Where(f => !f.IsConflicted).ToList();
        }
        var index = IndexOfSelection(Unstaged, SelectedUnstaged);
        return RunAsync("Stage", client => client.StageAsync(Paths(files)), Unstaged, index);
    }

    private Task UnstageAsync(IReadOnlyList<FileChangeViewModel> files)
    {
        var index = IndexOfSelection(Staged, SelectedStaged);
        return RunAsync("Unstage", client => client.UnstageAsync(Paths(files)), Staged, index);
    }

    private async Task DiscardAsync(IReadOnlyList<FileChangeViewModel> files)
    {
        if (files.Count == 0)
            return;

        // One file: the message already names it. Several: list them, marking the ones deleted.
        var items = files.Count == 1
            ? null
            : files.Select(f => f.IsUntracked ? f.Change.Path + "  (deleted)" : f.Change.Path).ToList();
        var confirm = new ConfirmDialogViewModel("Discard Changes", DiscardMessage(files), "Discard", items);
        if (!await _dialogs.ShowDialogAsync(confirm))
            return;

        var index = IndexOfSelection(Unstaged, SelectedUnstaged);
        await RunAsync("Discard", client => client.DiscardAsync(files.Select(f => f.Change).ToList()), Unstaged, index);
    }

    /// <summary>
    /// The Git rules: a destructive operation says exactly what will be lost, and only the
    /// sentences that apply (a new file is deleted; a tracked file goes back to an older version).
    /// </summary>
    private static string DiscardMessage(IReadOnlyList<FileChangeViewModel> files)
    {
        const string permanent = "deleted permanently (not moved to the Recycle Bin)";
        if (files.Count == 1)
        {
            var path = files[0].Change.Path;
            return files[0].IsUntracked
                ? $"Delete {path}? It isn't tracked by git, so it is {permanent}."
                : $"Discard your unstaged changes to {path}? It goes back to its staged or last committed version. This can't be undone.";
        }

        var untracked = files.Count(f => f.IsUntracked);
        var message = $"Discard the unstaged changes to {files.Count} files?";
        if (untracked < files.Count)
            message += " Tracked files go back to their staged or last committed version; staged changes are kept.";
        if (untracked > 0)
            message += untracked == 1 ? $" 1 untracked file is {permanent}." : $" {untracked} untracked files are {permanent}.";
        return message + " This can't be undone.";
    }

    /// <summary>
    /// Runs one operation at a time, reports failures in the status bar, refreshes, and then
    /// selects the row that moved into the place of the one acted on (like Fork), so pressing
    /// Space repeatedly stages file after file.
    /// </summary>
    private async Task RunAsync(string what, Func<GitRepositoryClient, Task> operation,
        ObservableCollection<FileChangeViewModel> list, int selectIndex)
    {
        if (_client is null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            await operation(_client);
        }
        catch (Exception ex)
        {
            _status.Show($"{what} failed: {(ex is GitException git && git.StandardError.Trim() is { Length: > 0 } e ? e : ex.Message)}");
        }
        finally
        {
            IsBusy = false;
        }

        await _refresh();
        if (selectIndex >= 0 && list.Count > 0 && Selected is null)
        {
            var next = list[Math.Min(selectIndex, list.Count - 1)];
            if (list == Unstaged)
                SelectedUnstaged = next;
            else
                SelectedStaged = next;
        }
    }

    private static int IndexOfSelection(ObservableCollection<FileChangeViewModel> list, FileChangeViewModel? selected) =>
        selected is null ? -1 : list.IndexOf(selected);

    /// <summary>A rename needs both paths, or git would only half-stage it.</summary>
    private static IEnumerable<string> Paths(IEnumerable<FileChangeViewModel> files) =>
        files.SelectMany(f => f.Change.OriginalPath is { } from ? [f.Change.Path, from] : new[] { f.Change.Path });

    partial void OnSelectedUnstagedChanged(FileChangeViewModel? value)
    {
        if (value is not null)
            SelectedStaged = null;
        SelectionChanged();
    }

    partial void OnSelectedStagedChanged(FileChangeViewModel? value)
    {
        if (value is not null)
            SelectedUnstaged = null;
        SelectionChanged();
    }

    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(Selected));
        // Picking a file in one list clears the other, which passes through here with the same
        // Selected: only load when it actually differs from what the pane shows. During Update
        // the lists are cleared and refilled; Update loads once at the end.
        if (!_updating && Selected != Diff.File)
            _ = Diff.LoadAsync(Selected);
    }

    private static void Fill(ObservableCollection<FileChangeViewModel> target, IEnumerable<FileChangeViewModel> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}

/// <summary>Kinds of change, for the row color (JetBrains: modified blue, added green, …).</summary>
public enum ChangeColor
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted,
}

/// <summary>A file row in Local Changes. In the Staged list it shows the staged status letter, in Unstaged the unstaged one.</summary>
public sealed class FileChangeViewModel
{
    /// <param name="toggleStage">Double-click: stage (Unstaged list) or unstage (Staged list) this row.</param>
    public FileChangeViewModel(FileChange change, bool staged, Action<FileChangeViewModel>? toggleStage = null)
    {
        Change = change;
        IsStagedSide = staged;
        ToggleStageCommand = new RelayCommand(() => toggleStage?.Invoke(this), () => toggleStage is not null);

        var letter = change.Kind switch
        {
            FileChangeKind.Untracked => '?',
            FileChangeKind.Unmerged => 'U',
            _ => staged ? change.IndexStatus : change.WorkTreeStatus,
        };
        StatusLetter = letter.ToString();
        Color = letter switch
        {
            '?' => ChangeColor.Untracked,
            'U' => ChangeColor.Conflicted,
            'A' => ChangeColor.Added,
            'D' => ChangeColor.Deleted,
            'R' or 'C' => ChangeColor.Renamed,
            _ => ChangeColor.Modified,
        };
        StatusText = Color switch
        {
            ChangeColor.Untracked => "Untracked",
            ChangeColor.Conflicted => "Conflict",
            ChangeColor.Added => "Added",
            ChangeColor.Deleted => "Deleted",
            ChangeColor.Renamed => "Renamed",
            _ => "Modified",
        };
    }

    public FileChange Change { get; }

    /// <summary>Double-click on the row (stage / unstage).</summary>
    public RelayCommand ToggleStageCommand { get; }

    /// <summary>True for the row in the Staged list.</summary>
    public bool IsStagedSide { get; }

    public string FileName => Change.FileName;
    public string Directory => Change.Directory;

    /// <summary>"M", "A", "D", "R", "?", "U".</summary>
    public string StatusLetter { get; }

    public string StatusText { get; }
    public ChangeColor Color { get; }

    // For style classes in the view (one bool per color).
    public bool IsModified => Color == ChangeColor.Modified;
    public bool IsAdded => Color == ChangeColor.Added;
    public bool IsDeleted => Color == ChangeColor.Deleted;
    public bool IsRenamed => Color == ChangeColor.Renamed;
    public bool IsUntracked => Color == ChangeColor.Untracked;
    public bool IsConflicted => Color == ChangeColor.Conflicted;

    /// <summary>Full path, plus where a rename came from.</summary>
    public string ToolTip => Change.OriginalPath is { } from
        ? $"{StatusText}: {from} → {Change.Path}"
        : $"{StatusText}: {Change.Path}";

    public override string ToString() => $"{StatusText} {Change.Path}";
}
