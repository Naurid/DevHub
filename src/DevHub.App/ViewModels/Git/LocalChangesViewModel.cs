using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Core.Git;

namespace DevHub.ViewModels.Git;

/// <summary>
/// Fork's Local Changes view for one repository tab: what changed, split into Unstaged (incl.
/// untracked and conflicts) and Staged, with the selected file's diff on the right (G2).
/// Read-only so far; staging and the commit box arrive in G3.
/// </summary>
public partial class LocalChangesViewModel : ViewModelBase
{
    /// <summary>True while <see cref="Update"/> refills the lists (selection changes then are temporary).</summary>
    private bool _updating;

    /// <param name="layout">Panel widths, shared by all repository tabs.</param>
    /// <param name="client">Git access for the diff pane (null when git isn't installed).</param>
    /// <param name="openInEditor">Opens a file in the external editor for the repository's language, at a line.</param>
    /// <param name="openInTab">Opens a file's diff as its own tab.</param>
    public LocalChangesViewModel(RepositoryTabLayout layout, GitRepositoryClient? client,
        Action<FileChangeViewModel, int?>? openInEditor, Action<FileChangeViewModel>? openInTab)
    {
        Layout = layout;
        Diff = new DiffViewModel(client, openInEditor, openInTab);
    }

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
    [ObservableProperty] private FileChangeViewModel? _selectedUnstaged;
    [ObservableProperty] private FileChangeViewModel? _selectedStaged;

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
            .Select(c => new FileChangeViewModel(c, staged: false)));
        Fill(Staged, status.Changes
            .Where(c => c.IsStaged)
            .OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .Select(c => new FileChangeViewModel(c, staged: true)));

        SelectedUnstaged = Unstaged.FirstOrDefault(f => f.Change.Path == keepUnstaged);
        SelectedStaged = Staged.FirstOrDefault(f => f.Change.Path == keepStaged);

        OnPropertyChanged(nameof(UnstagedCount));
        OnPropertyChanged(nameof(StagedCount));
        OnPropertyChanged(nameof(HasChanges));
    }

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
    public FileChangeViewModel(FileChange change, bool staged)
    {
        Change = change;
        IsStagedSide = staged;

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
