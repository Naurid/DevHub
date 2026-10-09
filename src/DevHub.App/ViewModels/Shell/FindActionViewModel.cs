using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Actions;
using DevHub.Core.Search;

namespace DevHub.ViewModels.Shell;

/// <summary>
/// Find Action (Ctrl+Shift+A, like JetBrains): type a few letters, every action is ranked by
/// <see cref="FuzzyMatcher"/>, the matched letters are highlighted, Enter runs the selection.
/// Shown as an overlay over the main window while <see cref="MainWindowViewModel.FindAction"/>
/// holds an instance; a fresh one per opening (transient in DI).
/// </summary>
public partial class FindActionViewModel : ViewModelBase
{
    private const int MaxResults = 50;

    /// <summary>Score penalty when only "Category: Text" matched, not the name itself.</summary>
    private const int CategoryOnlyPenalty = 10;

    private readonly ActionRegistry _registry;
    private readonly MainWindowViewModel _shell;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private FindActionItem? _selected;

    public ObservableCollection<FindActionItem> Results { get; } = new();

    public bool HasResults => Results.Count > 0;

    public FindActionViewModel(ActionRegistry registry, MainWindowViewModel shell)
    {
        _registry = registry;
        _shell = shell;
        Search();
    }

    partial void OnQueryChanged(string value) => Search();

    /// <summary>
    /// Ranks every action against the query. The name is matched first; if only
    /// "Category: Name" matches (e.g. "repo rescan"), it still counts, a little lower.
    /// Ties: enabled before disabled, then shorter names. Empty query = everything, A–Z.
    /// </summary>
    private void Search()
    {
        var ranked = _registry.All
            .Where(a => a.Id != ActionIds.FindAction)
            .Select(a => (Action: a, Rank: Rank(Query, a)))
            .Where(x => x.Rank is not null)
            .Select(x => new FindActionItem(x.Action, x.Rank!.Value.Score, x.Rank.Value.Positions))
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.IsEnabled)
            .ThenBy(i => i.Action.Text.Length)
            .ThenBy(i => i.Action.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults)
            .ToList();

        Results.Clear();
        foreach (var item in ranked)
            Results.Add(item);

        OnPropertyChanged(nameof(HasResults));
        Selected = Results.FirstOrDefault(i => i.IsEnabled) ?? Results.FirstOrDefault();
    }

    private static (int Score, IReadOnlyList<int> Positions)? Rank(string query, AppAction action)
    {
        if (FuzzyMatcher.Match(query, action.Text) is { } nameMatch)
            return (nameMatch.Score, nameMatch.Positions);

        if (action.Category is { } category && FuzzyMatcher.Match(query, action.DisplayName) is { } fullMatch)
        {
            // DisplayName is "Category: Text"; keep only positions inside Text for highlighting.
            var offset = category.Length + 2;
            var positions = fullMatch.Positions.Where(p => p >= offset).Select(p => p - offset).ToList();
            return (fullMatch.Score - CategoryOnlyPenalty, positions);
        }

        return null;
    }

    [RelayCommand]
    private void MoveDown() => Move(+1);

    [RelayCommand]
    private void MoveUp() => Move(-1);

    private void Move(int step)
    {
        if (Results.Count == 0)
            return;

        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        Selected = Results[Math.Clamp(index + step, 0, Results.Count - 1)];
    }

    /// <summary>Runs the given (or selected) action. Disabled actions do nothing and the popup stays open.</summary>
    [RelayCommand]
    private void Execute(FindActionItem? item)
    {
        item ??= Selected;
        if (item is null || !item.Action.Command.CanExecute(null))
            return;

        // Close first, so the action runs against the normal window (e.g. opens a dialog on top).
        Close();
        item.Action.Command.Execute(null);
    }

    [RelayCommand]
    private void Close() => _shell.FindAction = null;
}

/// <summary>One result row: the action, its score, and its name split into highlighted / plain runs.</summary>
public sealed class FindActionItem
{
    public FindActionItem(AppAction action, int score, IReadOnlyList<int> positions)
    {
        Action = action;
        Score = score;
        IsEnabled = action.Command.CanExecute(null);
        Segments = Split(action.Text, positions);
    }

    public AppAction Action { get; }
    public int Score { get; }

    /// <summary>Whether it can run right now (checked when the list was built).</summary>
    public bool IsEnabled { get; }

    /// <summary>The name in runs of matched / unmatched characters, e.g. [G][it: ][P][ush].</summary>
    public IReadOnlyList<TextSegment> Segments { get; }

    /// <summary>Accessible name (screen readers announce ToString for list items).</summary>
    public override string ToString() => Action.DisplayName;

    private static List<TextSegment> Split(string text, IReadOnlyList<int> positions)
    {
        var matched = new HashSet<int>(positions);
        var segments = new List<TextSegment>();
        var start = 0;

        for (var i = 1; i <= text.Length; i++)
        {
            // Close a run where the matched/unmatched state flips, or at the end.
            if (i == text.Length || matched.Contains(i) != matched.Contains(start))
            {
                segments.Add(new TextSegment(text[start..i], matched.Contains(start)));
                start = i;
            }
        }

        return segments;
    }
}

/// <summary>A run of text, highlighted when <see cref="IsMatch"/>.</summary>
public sealed record TextSegment(string Text, bool IsMatch);
