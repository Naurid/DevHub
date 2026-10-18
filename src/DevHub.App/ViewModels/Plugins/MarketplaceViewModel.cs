using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Plugins;
using DevHub.Sdk.Core.Plugins.Marketplace;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.ViewModels.Dialogs;
using DevHub.Sdk.Core.Localization;

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

    /// <summary>The plugins with a newer compatible version on offer (bundled ones included), for Update All.</summary>
    public IReadOnlyList<MarketplaceRowViewModel> Updatable => Rows.Where(r => r.HasUpdate && r.CanInstallNow).ToList();

    public bool HasUpdates => Updatable.Count > 0;

    public string UpdateAllText => L.T($"Update All ({Updatable.Count})");

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
            Message = listing.Problems.Count > 0 ? L.T("Couldn't read: ") + string.Join("; ", listing.Problems)
                : listing.Entries.Count == 0 ? L.T("The marketplace has no plugins yet.") : null;
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
            Message ??= L.T("Nothing installed from the marketplace yet. Refresh to see what it offers.");
        OnPropertyChanged(nameof(Updatable));
        OnPropertyChanged(nameof(HasUpdates));
        OnPropertyChanged(nameof(UpdateAllText));
        UpdateAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Every update at once: one confirmation listing them all (with where each comes from), then each plugin in turn,
    /// with what its new version requires. A plugin that fails is left as it was; the others still update.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasUpdates))]
    private async Task UpdateAllAsync()
    {
        var rows = Updatable;
        var plans = new List<(MarketplaceRowViewModel Row, InstallPlan Plan)>();
        var problems = new List<string>();
        foreach (var row in rows)
        {
            var plan = InstallPlanner.PlanInstall(row.Offered!, _offered, PresentIds());
            if (plan.Problem is not null)
                problems.Add($"{row.Name}: {plan.Problem}");
            else
                plans.Add((row, plan));
        }
        if (plans.Count == 0)
        {
            Message = string.Join(" ", problems);
            return;
        }

        var entries = plans.SelectMany(p => p.Plan.Install).DistinctBy(e => e.Id).ToList();
        var origins = Names(entries.Select(e => Place(e.Url)).Distinct().ToList());
        var ok = await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel(
            plans.Count == 1 ? L.T($"Update {plans[0].Row.Name}") : L.T($"Update {plans.Count} plugins"),
            L.T($"They come from {origins}.") + " "
            + L.T("A plugin is a program: it runs inside DevHub with your permissions. Install only if you trust where they come from. They're ready after a restart."),
            L.T("Update All"),
            entries.Select(e => $"{e.Name} {e.Version}: {e.Url}").ToList(), destructive: false));
        if (!ok)
            return;

        var updated = new List<IndexEntry>();
        foreach (var (row, plan) in plans)
        {
            // A requirement another update already brought along isn't installed twice.
            var install = plan.Install.Where(e => updated.All(u => u.Id != e.Id)).ToList();
            row.IsBusy = true;
            var error = await InstallAllOrNothingAsync(install);
            row.IsBusy = false;
            if (error is null)
                updated.AddRange(install);
            else
                problems.Add($"{row.Name}: {error}");
        }
        EnableAll(updated);
        Message = (updated.Count == 0 ? "" : L.T($"{Names(updated)}: updated, ready after a restart.") + " ")
                  + string.Join(" ", problems);
        if (updated.Count > 0)
            Pending();
        else
            ShowInstalled(_offered);
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
            ? L.T($"{entry.Name} {entry.Version} comes from {origins}.")
            : also.Count == 1
                ? L.T($"{entry.Name} {entry.Version} needs {Names(also)}, which is installed with it. They come from {origins}.")
                : L.T($"{entry.Name} {entry.Version} needs {Names(also)}, which are installed with it. They come from {origins}.");
        var ok = await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel(
            also.Count > 0 ? L.T($"Install {entry.Name} and {also.Count} more") : row.HasUpdate ? L.T($"Update {entry.Name}") : L.T($"Install {entry.Name}"),
            message + " " + (also.Count == 0
                ? L.T("A plugin is a program: it runs inside DevHub with your permissions. Install only if you trust where it comes from. It's ready after a restart.")
                : L.T("A plugin is a program: it runs inside DevHub with your permissions. Install only if you trust where they come from. They're ready after a restart.")),
            also.Count > 0 ? L.T("Install All") : row.HasUpdate ? L.T("Update") : L.T("Install"),
            plan.Install.Select(e => $"{e.Name} {e.Version}: {e.Url}").ToList(), destructive: false));
        if (!ok)
            return;

        row.IsBusy = true;
        var error = await InstallAllOrNothingAsync(plan.Install);
        row.IsBusy = false;
        if (error is not null)
        {
            Message = error;
            ShowInstalled(_offered);
            return;
        }
        EnableAll(plan.Install);
        Message = also.Count == 0
            ? L.T($"{entry.Name} {entry.Version} is ready: restart DevHub to use it.")
            : L.T($"{Names(plan.Install)} are ready: restart DevHub to use them.");
        Pending();
    }

    /// <summary>
    /// Installs the plugins of a plan in order; all or nothing: when one fails, those already unpacked for it are
    /// dropped again. Null when it worked, else what went wrong.
    /// </summary>
    private async Task<string?> InstallAllOrNothingAsync(IReadOnlyList<IndexEntry> install)
    {
        var done = new List<IndexEntry>();
        foreach (var item in install)
        {
            var error = await _installer.InstallAsync(item);
            if (error is not null)
            {
                foreach (var installed in done)
                    _installer.Remove(installed.Id);
                return done.Count == 0 ? error
                    : done.Count == 1 ? L.T($"{error} {Names(done)} wasn't installed either.")
                    : L.T($"{error} {Names(done)} weren't installed either.");
            }
            done.Add(item);
        }
        return null;
    }

    /// <summary>Switched on, so they load after the restart (null = everything is on anyway).</summary>
    private void EnableAll(IReadOnlyList<IndexEntry> installed) =>
        _store.Update(s =>
        {
            foreach (var item in installed)
            {
                if (s.EnabledPlugins is { } enabled && !enabled.Contains(item.Id))
                    enabled.Add(item.Id);
            }
        });

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
        names.Count <= 1 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + L.T(" and ") + names[^1];

    /// <summary>"github.com", or "this computer (C:\plugins\x.zip)" for a local index file.</summary>
    private static string Origin(string url) =>
        new Uri(url) is { IsFile: true } file ? L.T($"this computer ({file.LocalPath})") : new Uri(url).Host;

    /// <summary>"github.com", or "this computer".</summary>
    private static string Place(string url) => new Uri(url) is { IsFile: true } ? L.T("this computer") : new Uri(url).Host;

    private async Task RemoveAsync(MarketplaceRowViewModel row)
    {
        // Only an update of a bundled plugin: the plugin stays (its bundled version), nothing else is affected.
        if (row.Bundled is { } bundled)
        {
            _installer.Remove(row.Id);
            Message = row.IsOldCopy
                ? L.T($"The older copy of {row.Name} is removed when DevHub restarts (the bundled {bundled.Version} stays).")
                : L.T($"{row.Name} goes back to the bundled {bundled.Version} when DevHub restarts.");
            Pending();
            return;
        }

        // The installed plugins that need it would stop loading: they go too, after asking.
        var installed = _installer.List().Where(p => !p.PendingRemoval).Select(p => p.Info).ToList();
        var dependents = InstallPlanner.Dependents(row.Id, installed);
        if (dependents.Count > 0)
        {
            var names = dependents.Select(p => p.Name).ToList();
            var ok = await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel(L.T($"Remove {row.Name}"),
                names.Count == 1
                    ? L.T($"{Names(names)} needs {row.Name} and is removed with it, when DevHub restarts.")
                    : L.T($"{Names(names)} need {row.Name} and are removed with it, when DevHub restarts."),
                L.T("Remove All"), names));
            if (!ok)
                return;
            foreach (var plugin in dependents)
                _installer.Remove(plugin.Id);
        }
        _installer.Remove(row.Id);
        Message = dependents.Count == 0
            ? L.T($"{row.Name} is removed when DevHub restarts.")
            : L.T($"{Names(dependents.Select(p => p.Name).Prepend(row.Name).ToList())} are removed when DevHub restarts.");
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
        Name = L.T(name);
        Description = L.T(description ?? "");
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
    public string InstallText => HasUpdate ? L.T($"Update to {Offered!.Version}") : L.T("Install");

    /// <summary>Removing the installed copy of a bundled plugin only removes the update.</summary>
    public string RemoveText => Bundled is null ? L.T("Remove") : IsOldCopy ? L.T("Remove Old Copy") : L.T($"Back to {Bundled.Version}");

    /// <summary>"Installed 1.2.0 · 1.3.0 after restart", "Bundled 1.2.0", "Needs a newer DevHub"…</summary>
    public string StatusText =>
        Installed is { PendingRemoval: true } ? (Bundled is null ? L.T("Removed after the restart") : L.T($"Back to the bundled {Bundled.Version} after the restart"))
        : Installed is { PendingVersion: { } pending } ? (CurrentVersion is { } now ? L.T($"{(InstalledIsUsed ? L.T("Installed") : L.T("Bundled"))} {now} · {pending} after the restart") : L.T($"{pending} after the restart"))
        : InstalledIsUsed ? L.T($"Installed {Installed!.Info.Version}") + (Bundled is null ? "" : L.T($" · updates the bundled {Bundled.Version}")) + (Offered is null ? L.T(" · no longer offered") : "")
        : Bundled is not null ? L.T($"Bundled {Bundled.Version}") + (IsInstalled ? L.T($" · the installed {Installed!.Info.Version} is older, not used") : "")
        : Offered is { IsCompatible: false } ? L.T($"{Offered.Version}: made for another DevHub version")
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
