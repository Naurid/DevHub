using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Plugins;
using DevHub.Sdk.Core.Plugins.Marketplace;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.ViewModels.Dialogs;

namespace DevHub.ViewModels.Plugins;

/// <summary>
/// Settings → Plugins → Marketplace: the plugins the marketplace sources offer (docs/PLUGINS.md),
/// next to the ones installed from it. Install / Update / Remove take effect at the next start
/// (<see cref="PluginInstaller"/>); an installed plugin is switched on.
/// </summary>
public sealed partial class MarketplaceViewModel : ViewModelBase
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly PluginHost _host;
    private readonly ISettingsStore _store;
    private readonly IDialogService _dialogs;
    private readonly PluginInstaller _installer;
    private readonly Action _changed;
    private readonly Action _sourcesEdited;
    private IReadOnlyList<IndexEntry> _offered = [];

    /// <param name="changed">Something was installed or removed (the page shows its restart banner).</param>
    /// <param name="sourcesEdited">The sources were edited (the page saves them with Apply).</param>
    public MarketplaceViewModel(PluginHost host, ISettingsStore store, IDialogService dialogs, Action changed, Action sourcesEdited)
    {
        _sourcesEdited = sourcesEdited;
        _host = host;
        _store = store;
        _dialogs = dialogs;
        _changed = changed;
        _installer = new PluginInstaller(host.MarketplaceRoot, Http);
        _sources = string.Join(Environment.NewLine, store.Load().PluginSources ?? [PluginApi.DefaultIndexUrl]);
        ShowInstalled([]);
    }

    /// <summary>The index URLs (or local index.json files), one per line; saved with the page.</summary>
    [ObservableProperty] private string _sources;

    partial void OnSourcesChanged(string value) => _sourcesEdited();

    public ObservableCollection<MarketplaceRowViewModel> Rows { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _message;

    /// <summary>Installs or removals wait for a restart.</summary>
    [ObservableProperty] private bool _changesPending;

    public IReadOnlyList<string> SourceList =>
        Sources.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void SaveSources() =>
        _store.Update(s => s.PluginSources = SourceList.SequenceEqual([PluginApi.DefaultIndexUrl]) ? null : SourceList.ToList());

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var listing = await Marketplace.LoadAsync(Http, SourceList);
            _offered = listing.Entries;
            ShowInstalled(listing.Entries);
            Message = listing.Problems.Count > 0 ? "Couldn't read: " + string.Join("; ", listing.Problems)
                : listing.Entries.Count == 0 ? "The marketplace has no plugins yet." : null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>The rows: every offered plugin, plus installed ones the sources no longer list.</summary>
    private void ShowInstalled(IReadOnlyList<IndexEntry> offered)
    {
        var installed = _installer.List();
        Rows.Clear();
        foreach (var entry in offered)
        {
            var mine = installed.FirstOrDefault(p => p.Info.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase));
            Rows.Add(new MarketplaceRowViewModel(entry.Id, entry.Name, entry.Description, entry, mine, BundledCopy(entry.Id), InstallAsync, RemoveAsync));
        }
        foreach (var plugin in installed.Where(p => offered.All(e => !e.Id.Equals(p.Info.Id, StringComparison.OrdinalIgnoreCase))))
            Rows.Add(new MarketplaceRowViewModel(plugin.Info.Id, plugin.Info.Name, plugin.Info.Description, null, plugin, BundledCopy(plugin.Info.Id), InstallAsync, RemoveAsync));
        if (Rows.Count == 0 && offered.Count == 0)
            Message ??= "Nothing installed from the marketplace yet. Refresh to see what it offers.";
    }

    private async Task InstallAsync(MarketplaceRowViewModel row)
    {
        if (row.Offered is not { } entry)
            return;

        // What it requires and isn't there yet comes along (InstallPlanner), requirements first.
        var plan = InstallPlanner.PlanInstall(entry, _offered, PresentIds());
        if (plan.Problem is not null)
        {
            Message = plan.Problem;
            return;
        }
        var also = plan.AlsoInstalled;
        // One plugin: where exactly (a local file names its path). Several: the places only; the list below has each URL.
        var origins = also.Count == 0 ? Origin(entry.Url) : Names(plan.Install.Select(e => Place(e.Url)).Distinct().ToList());
        var message = also.Count == 0
            ? $"{entry.Name} {entry.Version} comes from {origins}."
            : $"{entry.Name} {entry.Version} needs {Names(also)}, which {(also.Count == 1 ? "is" : "are")} installed with it. They come from {origins}.";
        var ok = await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel(also.Count == 0 ? $"{(row.HasUpdate ? "Update" : "Install")} {entry.Name}" : $"Install {entry.Name} and {also.Count} more",
            message + $" A plugin is a program: it runs inside DevHub with your permissions. Install only if you trust where {(also.Count == 0 ? "it comes" : "they come")} from. {(also.Count == 0 ? "It's" : "They're")} ready after a restart.",
            also.Count > 0 ? "Install All" : row.HasUpdate ? "Update" : "Install",
            plan.Install.Select(e => $"{e.Name} {e.Version}: {e.Url}").ToList(), destructive: false));
        if (!ok)
            return;

        row.IsBusy = true;
        var done = new List<IndexEntry>();
        string? error = null;
        foreach (var item in plan.Install)
        {
            error = await _installer.InstallAsync(item);
            if (error is not null)
                break;
            done.Add(item);
        }
        row.IsBusy = false;
        if (error is not null)
        {
            // All or nothing: the requirements already unpacked for this install are dropped again.
            foreach (var item in done)
                _installer.Remove(item.Id);
            Message = done.Count > 0 ? $"{error} {Names(done)} {(done.Count == 1 ? "wasn't" : "weren't")} installed either." : error;
            if (done.Count > 0)
                ShowInstalled(_offered);
            return;
        }
        // Switched on, so they load after the restart (null = everything is on anyway).
        _store.Update(s =>
        {
            foreach (var item in plan.Install)
            {
                if (s.EnabledPlugins is { } enabled && !enabled.Contains(item.Id))
                    enabled.Add(item.Id);
            }
        });
        Message = also.Count == 0
            ? $"{entry.Name} {entry.Version} is ready: restart DevHub to use it."
            : $"{Names(plan.Install)} are ready: restart DevHub to use them.";
        Pending();
    }

    /// <summary>Plugins there at the next start: built in, bundled, or installed from the marketplace (and not being removed).</summary>
    private IEnumerable<string> PresentIds() =>
        _host.Available.Select(p => p.Info.Id).Where(id => !_host.MarketplaceIds.Contains(id))
            .Concat(_host.Bundled.Select(b => b.Id))
            .Concat(_installer.List().Where(p => !p.PendingRemoval).Select(p => p.Info.Id));

    private PluginPackageInfo? BundledCopy(string id) =>
        _host.Bundled.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>"Git", "Git and Projects", "Git, Projects and Templates".</summary>
    private static string Names(IReadOnlyList<IndexEntry> entries) => Names(entries.Select(e => e.Name).ToList());

    private static string Names(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

    /// <summary>"github.com", or "this computer (C:\plugins\x.zip)" for a local index file.</summary>
    private static string Origin(string url) =>
        new Uri(url) is { IsFile: true } file ? $"this computer ({file.LocalPath})" : new Uri(url).Host;

    /// <summary>"github.com", or "this computer".</summary>
    private static string Place(string url) => new Uri(url) is { IsFile: true } ? "this computer" : new Uri(url).Host;

    private async Task RemoveAsync(MarketplaceRowViewModel row)
    {
        // Only an update of a bundled plugin: the plugin stays (its bundled version), nothing else is affected.
        if (row.Bundled is { } bundled)
        {
            _installer.Remove(row.Id);
            Message = row.IsOldCopy
                ? $"The older copy of {row.Name} is removed when DevHub restarts (the bundled {bundled.Version} stays)."
                : $"{row.Name} goes back to the bundled {bundled.Version} when DevHub restarts.";
            Pending();
            return;
        }

        // The installed plugins that need it would stop loading: they go too, after asking.
        var installed = _installer.List().Where(p => !p.PendingRemoval).Select(p => p.Info).ToList();
        var dependents = InstallPlanner.Dependents(row.Id, installed);
        if (dependents.Count > 0)
        {
            var names = dependents.Select(p => p.Name).ToList();
            var ok = await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel($"Remove {row.Name}",
                $"{Names(names)} {(names.Count == 1 ? "needs" : "need")} {row.Name} and {(names.Count == 1 ? "is" : "are")} removed with it, when DevHub restarts.",
                "Remove All", names));
            if (!ok)
                return;
            foreach (var plugin in dependents)
                _installer.Remove(plugin.Id);
        }
        _installer.Remove(row.Id);
        Message = dependents.Count == 0
            ? $"{row.Name} is removed when DevHub restarts."
            : $"{Names(dependents.Select(p => p.Name).Prepend(row.Name).ToList())} are removed when DevHub restarts.";
        Pending();
    }

    private void Pending()
    {
        ChangesPending = true;
        ShowInstalled(_offered);
        _changed();
    }
}

/// <summary>One marketplace plugin: what's offered, what's installed, what's waiting for a restart.</summary>
public sealed partial class MarketplaceRowViewModel : ViewModelBase
{
    private readonly Func<MarketplaceRowViewModel, Task> _install;
    private readonly Func<MarketplaceRowViewModel, Task> _remove;

    /// <param name="bundled">The copy that comes with DevHub, if it bundles this plugin: an installed copy is then an
    ///                       update of it (used while newer), and removing it goes back to the bundled version.</param>
    public MarketplaceRowViewModel(string id, string name, string? description, IndexEntry? offered, InstalledPlugin? installed,
        PluginPackageInfo? bundled, Func<MarketplaceRowViewModel, Task> install, Func<MarketplaceRowViewModel, Task> remove)
    {
        Id = id;
        Name = name;
        Description = description ?? "";
        Offered = offered;
        Installed = installed;
        Bundled = bundled;
        _install = install;
        _remove = remove;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public IndexEntry? Offered { get; }
    public InstalledPlugin? Installed { get; }
    public PluginPackageInfo? Bundled { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(RemoveCommand))]
    private bool _isBusy;

    private bool IsInstalled => Installed is { PendingRemoval: false } && Installed.Info.Version.Length > 0;
    private bool IsWaiting => Installed is { PendingVersion: not null } || Installed is { PendingRemoval: true };

    /// <summary>The installed copy is used: there's no bundled one, or it's newer (PluginFolders.Choose).</summary>
    private bool InstalledIsUsed => IsInstalled && (Bundled is null || Marketplace.IsNewer(Installed!.Info.Version, Bundled.Version));

    /// <summary>An installed copy that isn't used: DevHub bundles a version at least as new.</summary>
    public bool IsOldCopy => Bundled is not null && IsInstalled && !InstalledIsUsed;

    /// <summary>The version DevHub runs now: the installed copy's while it's used, else the bundled one's.</summary>
    private string? CurrentVersion => InstalledIsUsed ? Installed!.Info.Version : Bundled?.Version;

    public bool HasUpdate => CurrentVersion is { } current && Offered is { IsCompatible: true } offered && Marketplace.IsNewer(offered.Version, current);
    public bool CanInstallNow => Offered is { IsCompatible: true } && !IsWaiting && (CurrentVersion is null || HasUpdate);
    public bool CanRemoveNow => Installed is { PendingRemoval: false };
    public string InstallText => HasUpdate ? $"Update to {Offered!.Version}" : "Install";

    /// <summary>Removing the installed copy of a bundled plugin only removes the update.</summary>
    public string RemoveText => Bundled is null ? "Remove" : IsOldCopy ? "Remove Old Copy" : $"Back to {Bundled.Version}";

    /// <summary>"Installed 1.2.0 · 1.3.0 after restart", "Bundled 1.2.0", "Needs a newer DevHub"…</summary>
    public string StatusText =>
        Installed is { PendingRemoval: true } ? (Bundled is null ? "Removed after the restart" : $"Back to the bundled {Bundled.Version} after the restart")
        : Installed is { PendingVersion: { } pending } ? (CurrentVersion is { } now ? $"{(InstalledIsUsed ? "Installed" : "Bundled")} {now} · {pending} after the restart" : $"{pending} after the restart")
        : InstalledIsUsed ? $"Installed {Installed!.Info.Version}" + (Bundled is null ? "" : $" · updates the bundled {Bundled.Version}") + (Offered is null ? " · no longer offered" : "")
        : Bundled is not null ? $"Bundled {Bundled.Version}" + (IsInstalled ? $" · the installed {Installed!.Info.Version} is older, not used" : "")
        : Offered is { IsCompatible: false } ? $"{Offered.Version}: made for another DevHub version"
        : Offered is { } offered ? offered.Version
        : "";

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallAsync() => _install(this);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveAsync() => _remove(this);

    private bool CanInstall() => CanInstallNow && !IsBusy;
    private bool CanRemove() => CanRemoveNow && !IsBusy;

    public override string ToString() => $"{Name}, {StatusText}";
}
