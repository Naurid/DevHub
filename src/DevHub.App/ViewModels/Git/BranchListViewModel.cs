using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Core.Formatting;
using DevHub.Core.Git;
using DevHub.Core.Search;

namespace DevHub.ViewModels.Git;

/// <summary>
/// The Branches section (local branches) or the Remotes section (remote-tracking branches,
/// grouped by remote) of a repository tab: a filterable list with the current branch marked,
/// ahead/behind, and the last commit. Double-click / Enter checks out.
/// </summary>
public sealed partial class BranchListViewModel : ViewModelBase
{
    private readonly BranchOperations _operations;
    private IReadOnlyList<GitBranch> _branches = [];

    public BranchListViewModel(BranchOperations operations, bool remote)
    {
        _operations = operations;
        IsRemote = remote;
    }

    /// <summary>Remotes section (else Branches).</summary>
    public bool IsRemote { get; }

    /// <summary>Manage Remotes / Publish to GitHub (the Remotes section's header buttons); set by the tab.</summary>
    public RemoteOperations? Remote { get; set; }

    public string Title => IsRemote ? "Remote Branches" : "Local Branches";

    public ObservableCollection<BranchItemViewModel> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    /// <summary>Shown when there's nothing (or nothing matching).</summary>
    public string EmptyText => !string.IsNullOrWhiteSpace(Filter) ? "No branch matches."
        : IsRemote ? "No remote branches. Add a remote (Manage Remotes…) and Fetch, or Publish to GitHub… to create one there."
        : "No branches yet: make the first commit.";

    /// <summary>Number of branches (sidebar badge), regardless of the filter.</summary>
    public int Count => _branches.Count;

    [ObservableProperty] private string _filter = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand), nameof(NewBranchFromCommand), nameof(RenameCommand), nameof(DeleteCommand),
        nameof(MergeIntoCurrentCommand), nameof(RebaseCurrentOntoCommand))]
    private BranchItemViewModel? _selected;

    partial void OnFilterChanged(string value) => Rebuild();

    /// <summary>Takes the freshly loaded branches (this section's kind only), keeping the selection.</summary>
    public void Update(IReadOnlyList<GitBranch> all)
    {
        _branches = all.Where(b => b.IsRemote == IsRemote).ToList();
        Rebuild();
        OnPropertyChanged(nameof(Count));
    }

    private void Rebuild()
    {
        var keep = Selected?.Branch.Ref;
        var now = DateTimeOffset.Now;
        var filtering = !string.IsNullOrWhiteSpace(Filter);

        var rows = _branches
            .Select(b => (Branch: b, Match: filtering ? FuzzyMatcher.Match(Filter, b.ShortName) : null))
            .Where(x => !filtering || x.Match is not null)
            // Filtering: best match first. Otherwise: current branch first, then by name (remotes grouped by remote).
            .OrderByDescending(x => x.Match?.Score ?? 0)
            .ThenByDescending(x => !filtering && x.Branch.IsCurrent)
            .ThenBy(x => x.Branch.ShortName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new BranchItemViewModel(x.Branch, now, item => _ = _operations.CheckoutAsync(item.Branch)));

        Items.Clear();
        foreach (var row in rows)
            Items.Add(row);

        Selected = Items.FirstOrDefault(i => i.Branch.Ref == keep);
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(EmptyText));
    }

    private bool HasSelection() => Selected is not null && _operations.IsAvailable;
    private bool CanCheckout() => Selected is { Branch.IsCurrent: false } && _operations.IsAvailable;
    private bool CanEditLocal() => Selected is { Branch.IsRemote: false } && _operations.IsAvailable;
    private bool CanDelete() => Selected is { Branch: { IsRemote: false, IsCurrent: false } } && _operations.IsAvailable;

    /// <summary>Enter / double-click / "Checkout".</summary>
    [RelayCommand(CanExecute = nameof(CanCheckout))]
    private Task CheckoutAsync() => _operations.CheckoutAsync(Selected!.Branch);

    /// <summary>New branch from the current commit.</summary>
    [RelayCommand]
    private Task NewBranchAsync() => _operations.NewBranchAsync();

    /// <summary>New branch starting at the selected branch.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task NewBranchFromAsync() => _operations.NewBranchAsync(Selected!.Branch);

    [RelayCommand(CanExecute = nameof(CanEditLocal))]
    private Task RenameAsync() => _operations.RenameAsync(Selected!.Branch);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync() => _operations.DeleteAsync(Selected!.Branch);

    /// <summary>Merge the selected branch into the current one (G7).</summary>
    [RelayCommand(CanExecute = nameof(CanCheckout))]
    private Task MergeIntoCurrentAsync() => _operations.MergeIntoCurrentAsync(Selected!.Branch);

    /// <summary>Replay the current branch's commits on top of the selected one (G7).</summary>
    [RelayCommand(CanExecute = nameof(CanCheckout))]
    private Task RebaseCurrentOntoAsync() => _operations.RebaseCurrentOntoAsync(Selected!.Branch);
}

/// <summary>A branch row: name (bold when current), ahead/behind, last commit and when.</summary>
public sealed class BranchItemViewModel
{
    public BranchItemViewModel(GitBranch branch, DateTimeOffset now, Action<BranchItemViewModel> checkout)
    {
        Branch = branch;
        When = branch.CommitDate == default ? "" : RelativeTime.Format(branch.CommitDate, now);
        SyncText = branch.UpstreamGone ? "gone"
            : (branch.Ahead, branch.Behind) switch
            {
                (0, 0) => null,
                (var a, 0) => $"↑{a}",
                (0, var b) => $"↓{b}",
                var (a, b) => $"↑{a} ↓{b}",
            };
        CheckoutCommand = new RelayCommand(() => checkout(this), () => !branch.IsCurrent);
    }

    public GitBranch Branch { get; }

    /// <summary>"main", or "origin/main" in the Remotes section.</summary>
    public string Name => Branch.ShortName;

    public bool IsCurrent => Branch.IsCurrent;

    /// <summary>"↑1 ↓2" against the upstream, "gone" when the upstream was deleted, or null.</summary>
    public string? SyncText { get; }

    public string Subject => Branch.Subject;
    public string When { get; }

    /// <summary>Double-click on the row.</summary>
    public RelayCommand CheckoutCommand { get; }

    public string ToolTip => Branch.Upstream is { } up
        ? $"{Branch.ShortName} → {up}{(Branch.UpstreamGone ? " (deleted on the remote)" : "")}\n{Branch.CommitId[..Math.Min(10, Branch.CommitId.Length)]} {Branch.Subject}"
        : $"{Branch.ShortName}\n{Branch.CommitId[..Math.Min(10, Branch.CommitId.Length)]} {Branch.Subject}";

    public override string ToString() => IsCurrent ? $"{Name} (current)" : Name;
}
