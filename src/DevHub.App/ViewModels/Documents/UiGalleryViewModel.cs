using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Sdk.Core.Charts;
using DevHub.Sdk.Core.Collections;
using DevHub.Sdk.Core.People;
using DevHub.Services;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// Debug-only page showing every token and stock control in the current theme, with
/// switches for Dark/Light, Compact and accent. Use it to check design-system changes.
/// </summary>
public partial class UiGalleryViewModel : DocumentViewModelBase, IItemsDropHandler, IDisposable
{
    private readonly IThemeService _theme;

    public override string Title => "UI Gallery";
    public override string Icon => IconKind.Palette;
    public override DocumentNavigation Navigation => new() { Menu = "View", MenuOrder = 400 };

    [ObservableProperty] private bool _isDark;
    [ObservableProperty] private bool _isCompact;
    [ObservableProperty] private string _accent;

    /// <summary>Every vendored icon name.</summary>
    public IReadOnlyList<string> IconKinds => IconKind.All;

    /// <summary>Sample rows for the list/combo demos.</summary>
    public IReadOnlyList<string> SampleItems { get; } =
        ["DevHub", "Timber (Unity)", "website (Tauri)", "scripts (Python)", "a-very-long-repository-name-to-test-trimming"];

    // ── Sdk building blocks for plugins (PM6): Markdown, charts, a board, a people picker ──

    public string SampleMarkdown { get; } = """
        ## Release notes
        DevHub **1.6** brings *plugins everywhere*: see [the plugin guide](https://github.com/Naurid/DevHub) or `docs/PLUGINS.md`.

        - [x] Sdk on nuget.org
        - [ ] Board plugin
          - columns you can **drag**
        1. first
        2. second

        > Plugins run with your permissions. Install what you trust.

        ```csharp
        services.AddSingleton<IGitDecorator, CiDecorator>();
        ```
        ---
        A link that stays text: [local file](file:///C:/Windows), and an autolink <https://nuget.org>.
        """;

    public ChartData Burndown { get; } = new(
        ["Mon", "Tue", "Wed", "Thu", "Fri", "Mon", "Tue", "Wed", "Thu", "Fri"],
        [new ChartSeries("Remaining", [40, 37, 35, 30, 30, 24, 18, 12, null, null]),
         new ChartSeries("Ideal", [40, 35.6, 31.1, 26.7, 22.2, 17.8, 13.3, 8.9, 4.4, 0])]);

    public ChartData Velocity { get; } = new(
        ["Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5"],
        [new ChartSeries("Committed", [21, 25, 24, 30, 28]), new ChartSeries("Done", [18, 25, 20, 31, 27])]);

    public ChartData ByStatus { get; } = ChartData.Of("Issues", [("To Do", 12), ("In Progress", 5), ("In Review", 3), ("Done", 31)]);

    public ChartData NoData { get; } = ChartData.Empty;

    /// <summary>A board to try ItemsDragDrop on: drag cards between columns, and columns along the board.</summary>
    public ObservableCollection<GalleryColumn> Columns { get; } =
    [
        new("To Do", ["DEV-12 Board plugin", "DEV-14 Charts", "DEV-15 People picker"]),
        new("In Progress", ["DEV-9 Markdown viewer"]),
        new("Done", ["DEV-1 Sdk on NuGet", "DEV-3 Marketplace"]),
    ];

    public IReadOnlyList<Person> Team { get; } =
    [
        new("naurid", "Naurid"), new("ada", "Ada Lovelace", "ada@example.com"), new("ghopper", "Grace Hopper"),
        new("torvalds", "Linus Torvalds"), new("kay", "Alan Kay"), new("bliskov", "Barbara Liskov"),
    ];

    public ObservableCollection<Person> Assignees { get; } = [];

    public ObservableCollection<Person> Reviewer { get; } = [];

    public bool CanDrop(ItemsDrop drop) => drop switch
    {
        { Item: GalleryColumn, TargetList: ObservableCollection<GalleryColumn> } => true,
        { Item: GalleryCard, TargetList: GalleryColumn } => true,
        _ => false,
    };

    public void Drop(ItemsDrop drop)
    {
        switch (drop)
        {
            case { Item: GalleryColumn column }:
                ListMoves.Move(Columns, column, Columns, drop.Index);
                break;
            case { Item: GalleryCard card, TargetList: GalleryColumn target }
                when Columns.FirstOrDefault(c => c.Cards.Contains(card)) is { } source:
                ListMoves.Move(source.Cards, card, target.Cards, drop.Index);
                break;
        }
    }

    public UiGalleryViewModel(IThemeService theme)
    {
        _theme = theme;
        _isDark = theme.IsDark;
        _isCompact = theme.IsCompact;
        _accent = theme.Accent;

        // The theme can also change from elsewhere (e.g. the Ctrl+Alt+D action);
        // keep the switches in sync. Unsubscribed in Dispose.
        _theme.Changed += OnThemeChanged;
    }

    // Setting a property to the value it already has does nothing, and ThemeService ignores
    // no-op changes, so these two directions can't loop.
    partial void OnIsDarkChanged(bool value) => _theme.IsDark = value;

    partial void OnIsCompactChanged(bool value) => _theme.IsCompact = value;

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        IsDark = _theme.IsDark;
        IsCompact = _theme.IsCompact;
        Accent = _theme.Accent;
    }

    [RelayCommand]
    private void SetAccent(string hex) => _theme.SetAccent(hex);

    /// <summary>Called by the DI container when it is disposed (app shutdown).</summary>
    public void Dispose() => _theme.Changed -= OnThemeChanged;
}

/// <summary>A column of the gallery's sample board.</summary>
public sealed class GalleryColumn(string name, IEnumerable<string> cards)
{
    public string Name { get; } = name;
    public ObservableCollection<GalleryCard> Cards { get; } = new(cards.Select(c => new GalleryCard(c)));
    public override string ToString() => Name;
}

/// <summary>A card of the sample board (a class: two cards with the same text are still two cards).</summary>
public sealed class GalleryCard(string title)
{
    public string Title { get; } = title;
    public override string ToString() => Title;
}
