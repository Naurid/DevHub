using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevHub.Core.Search;

namespace DevHub.ViewModels.Dialogs.Settings;

/// <summary>
/// Settings dialog (Ctrl+Alt+S), JetBrains-style: a searchable page list on the left, the
/// selected page on the right, OK / Cancel / Apply at the bottom. Pages edit copies; Apply
/// (or OK) saves every modified page, Cancel throws the edits away.
/// </summary>
/// <remarks>
/// Created fresh each time the dialog opens (transient in DI), so it always starts from the
/// saved values.
/// </remarks>
public partial class SettingsDialogViewModel : DialogViewModelBase
{
    public override string Title => "Settings";
    public override double Width => 880;
    public override double Height => 600;
    public override bool ShowApply => true;

    /// <summary>All pages, in list order.</summary>
    public IReadOnlyList<SettingsPageViewModelBase> Pages { get; }

    /// <summary>Pages matching the search box (all of them when it's empty).</summary>
    public ObservableCollection<SettingsPageViewModelBase> VisiblePages { get; } = new();

    [ObservableProperty] private SettingsPageViewModelBase? _selectedPage;

    /// <summary>Search box text: fuzzy-matches page titles and keywords ("acc" finds Appearance via "accent").</summary>
    [ObservableProperty] private string _filter = "";

    public SettingsDialogViewModel(AppearanceSettingsPageViewModel appearance, OpenWithSettingsPageViewModel openWith)
    {
        Pages = [appearance, openWith];
        foreach (var page in Pages)
            page.Modified = () => ApplyCommand.NotifyCanExecuteChanged();

        ApplyFilter();
        SelectedPage = Pages[0];
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        // Match the title and each keyword separately. One long "title + keywords" string lets
        // the letters scatter across words: "rider" matched Appearance via
        // da(r)k l(i)ght win(d)ows acc(e)nt colo(r).
        var matches = Pages
            .Where(p => p.Keywords
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Prepend(p.Title)
                .Any(word => FuzzyMatcher.Match(Filter, word) is not null))
            .ToList();

        VisiblePages.Clear();
        foreach (var page in matches)
            VisiblePages.Add(page);

        if (SelectedPage is null || !VisiblePages.Contains(SelectedPage))
            SelectedPage = VisiblePages.FirstOrDefault();
    }

    protected override bool CanApply() => Pages.Any(p => p.IsModified);

    protected override Task<bool> ApplyChangesAsync()
    {
        foreach (var page in Pages.Where(p => p.IsModified))
            page.Apply();
        return Task.FromResult(true);
    }
}
