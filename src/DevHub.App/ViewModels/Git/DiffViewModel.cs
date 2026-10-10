using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Core.Git;

namespace DevHub.ViewModels.Git;

/// <summary>
/// The diff of one changed file, as rows for the view (unified: hunk headers, then context /
/// removed / added lines with old and new line numbers). Used by the Local Changes diff pane
/// and by a diff opened as its own editor tab. Side-by-side arrives later.
/// </summary>
public partial class DiffViewModel : ViewModelBase
{
    private readonly GitRepositoryClient? _client;
    private readonly Action<FileChangeViewModel, int?>? _openInEditor;
    private readonly Action<FileChangeViewModel>? _openInTab;
    private CancellationTokenSource? _loadCts;

    /// <summary>Where the first change is, so the editor opens there.</summary>
    private int? _firstChangedLine;

    /// <param name="client">Git access, or null when git isn't installed.</param>
    /// <param name="openInEditor">Opens the file in the external editor for the repository's language, at a line.</param>
    /// <param name="openInTab">Opens this diff as its own tab; null hides the button (e.g. in that tab).</param>
    public DiffViewModel(GitRepositoryClient? client, Action<FileChangeViewModel, int?>? openInEditor = null,
        Action<FileChangeViewModel>? openInTab = null)
    {
        _client = client;
        _openInEditor = openInEditor;
        _openInTab = openInTab;
    }

    /// <summary>The file shown, or null (nothing selected).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile), nameof(PlaceholderText))]
    [NotifyCanExecuteChangedFor(nameof(OpenInEditorCommand), nameof(OpenInTabCommand))]
    private FileChangeViewModel? _file;

    public bool HasFile => File is not null;

    /// <summary>"Staged" / "Unstaged" (+ "renamed from …"), under the file name.</summary>
    [ObservableProperty] private string? _subtitle;

    /// <summary>"+12" and "−3" in the header; null when there are no line changes.</summary>
    [ObservableProperty] private string? _addedText;
    [ObservableProperty] private string? _removedText;

    /// <summary>The lines. Replaced as a whole on each load (one change notification, not thousands).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows), nameof(BannerText), nameof(PlaceholderText))]
    private IReadOnlyList<DiffRowViewModel> _rows = [];

    public bool HasRows => Rows.Count > 0;

    /// <summary>Binary file, folder, no changes, an error, or "cut off" (the only one that comes with lines).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerText), nameof(PlaceholderText))]
    private string? _message;

    /// <summary>The message as a strip above the lines (when there are lines).</summary>
    public string? BannerText => HasRows ? Message : null;

    /// <summary>
    /// What the pane says when there are no lines: the message, or a hint when nothing is
    /// selected. Empty while the first diff loads (a "Loading…" flash would only flicker).
    /// </summary>
    public string? PlaceholderText => HasRows ? null : Message ?? (HasFile ? null : "Select a file to see its changes.");

    public bool CanOpenInEditor => _openInEditor is not null;
    public bool CanOpenInTab => _openInTab is not null;

    /// <summary>
    /// Shows <paramref name="file"/>'s diff (null clears the pane). Called whenever the selection
    /// changes and after every status refresh (the list items are new objects then), so the
    /// diff follows edits made outside DevHub.
    /// </summary>
    public Task LoadAsync(FileChangeViewModel? file)
    {
        var client = _client;
        return LoadCoreAsync(file,
            client is null ? null : ct => client.GetDiffAsync(file!.Change, file.IsStagedSide, ct),
            file is null ? "" : file.IsConflicted ? "Conflict" : file.IsStagedSide ? "Staged" : "Unstaged");
    }

    /// <summary>Shows a file as a commit changed it (All Commits), compared with the commit's first parent.</summary>
    public Task LoadCommitFileAsync(GitCommit commit, FileChangeViewModel? file)
    {
        var client = _client;
        return LoadCoreAsync(file,
            client is null ? null : ct => client.GetCommitDiffAsync(commit, file!.Change, ct),
            $"Commit {commit.ShortId}");
    }

    private async Task LoadCoreAsync(FileChangeViewModel? file, Func<CancellationToken, Task<FileDiff>>? load, string side)
    {
        _loadCts?.Cancel();
        File = file;

        if (file is null || load is null)
        {
            Show(null, null, "");
            return;
        }

        var cts = _loadCts = new CancellationTokenSource();
        try
        {
            var diff = await load(cts.Token);
            if (!cts.IsCancellationRequested)
                Show(file, diff, side);
        }
        catch (OperationCanceledException)
        {
            // A newer load replaced this one.
        }
        catch (Exception ex)
        {
            // Fire-and-forget caller: show the problem in the pane instead of losing it.
            Rows = [];
            Message = "Couldn't load the diff: " + ex.Message;
        }
    }

    /// <summary>Clears the pane and shows a message instead (e.g. "no changes any more").</summary>
    public void ShowMessage(string message)
    {
        _loadCts?.Cancel();
        File = null;
        Show(null, null, "");
        Message = message;
    }

    /// <summary>"Open in Editor" (F4, JetBrains' Jump to Source): the real file in the external editor.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private void OpenInEditor()
    {
        if (File is not null)
            _openInEditor?.Invoke(File, _firstChangedLine);
    }

    // A deleted file or an untracked folder has nothing to open.
    private bool CanOpenFile() => File is { IsDeleted: false } file && !file.Change.Path.EndsWith('/') && _openInEditor is not null;

    /// <summary>"Open in New Tab": this diff as its own tab, for more room.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenTab))]
    private void OpenInTab()
    {
        if (File is not null)
            _openInTab?.Invoke(File);
    }

    private bool CanOpenTab() => File is not null && _openInTab is not null;

    /// <param name="side">Where the diff comes from, for the subtitle: "Staged", "Unstaged", "Commit 1a2b3c4d"…</param>
    private void Show(FileChangeViewModel? file, FileDiff? diff, string side)
    {
        _firstChangedLine = diff?.FirstChangedLine;
        if (file is null || diff is null)
        {
            Subtitle = AddedText = RemovedText = Message = null;
            Rows = [];
            return;
        }

        Subtitle = diff.IsRenamed ? $"{side} · renamed from {diff.OldPath}" : side;
        AddedText = diff.AddedCount > 0 ? $"+{diff.AddedCount}" : null;
        RemovedText = diff.RemovedCount > 0 ? $"−{diff.RemovedCount}" : null;
        Rows = BuildRows(diff);
        Message = diff switch
        {
            { IsDirectory: true } => "New folder, not tracked yet: git lists it as one entry. Its files show up one by one once the folder is staged.",
            { IsBinary: true } => "Binary file: no text to compare.",
            { ModeChange: { } mode, Hunks.Count: 0 } => $"Only the file mode changed ({mode}).",
            { IsRenamed: true, Hunks.Count: 0 } => "Renamed without changes to the content.",
            { IsTruncated: true } => $"Long diff: showing the first {DiffParser.DefaultMaxLines:N0} lines.",
            { Hunks.Count: 0 } => "No changes to show.",
            _ => null,
        };
    }

    private static List<DiffRowViewModel> BuildRows(FileDiff diff)
    {
        var rows = new List<DiffRowViewModel>();
        foreach (var hunk in diff.Hunks)
        {
            rows.Add(DiffRowViewModel.ForHunk(hunk));
            foreach (var line in hunk.Lines)
            {
                rows.Add(DiffRowViewModel.ForLine(line));
                if (line.NoNewlineAtEnd)
                    rows.Add(DiffRowViewModel.ForNote("No newline at end of file"));
            }
        }
        return rows;
    }
}

/// <summary>One row of the diff view: a hunk header, a line, or a note.</summary>
public sealed class DiffRowViewModel
{
    private DiffRowViewModel(string oldNumber, string newNumber, string sign, string text)
    {
        OldNumber = oldNumber;
        NewNumber = newNumber;
        Sign = sign;
        Text = text;
    }

    public string OldNumber { get; }
    public string NewNumber { get; }

    /// <summary>"+", "-" or "" (shown in its own column, like JetBrains' unified view).</summary>
    public string Sign { get; }

    public string Text { get; }

    public bool IsHunk { get; private init; }
    public bool IsAdded { get; private init; }
    public bool IsRemoved { get; private init; }
    public bool IsNote { get; private init; }

    /// <summary>Shown in the secondary color (hunk headers, notes).</summary>
    public bool IsMuted => IsHunk || IsNote;

    public static DiffRowViewModel ForHunk(DiffHunk hunk) =>
        new("", "", "", hunk.Header) { IsHunk = true };

    public static DiffRowViewModel ForNote(string text) =>
        new("", "", "", text) { IsNote = true };

    public static DiffRowViewModel ForLine(DiffLine line) =>
        new(line.OldLineNumber?.ToString() ?? "", line.NewLineNumber?.ToString() ?? "",
            line.Kind switch { DiffLineKind.Added => "+", DiffLineKind.Removed => "-", _ => "" },
            // TextBlock doesn't do tab stops; 4 spaces keep indentation readable.
            line.Text.Replace("\t", "    ", StringComparison.Ordinal))
        {
            IsAdded = line.Kind == DiffLineKind.Added,
            IsRemoved = line.Kind == DiffLineKind.Removed,
        };

    public override string ToString() => $"{Sign}{Text}";
}
