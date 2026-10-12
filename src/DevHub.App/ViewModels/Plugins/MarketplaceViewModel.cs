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
            Rows.Add(new MarketplaceRowViewModel(entry.Id, entry.Name, entry.Description, entry, mine, InstallAsync, Remove));
        }
        foreach (var plugin in installed.Where(p => offered.All(e => !e.Id.Equals(p.Info.Id, StringComparison.OrdinalIgnoreCase))))
            Rows.Add(new MarketplaceRowViewModel(plugin.Info.Id, plugin.Info.Name, plugin.Info.Description, null, plugin, InstallAsync, Remove));
        if (Rows.Count == 0 && offered.Count == 0)
            Message ??= "Nothing installed from the marketplace yet. Refresh to see what it offers.";
    }

    private async Task InstallAsync(MarketplaceRowViewModel row)
    {
        if (row.Offered is not { } entry)
            return;
        var ok = await _dialogs.ShowDialogAsync(new ConfirmDialogViewModel($"Install {entry.Name}",
            $"{entry.Name} {entry.Version} comes from {new Uri(entry.Url).Host}. A plugin is a program: it runs inside DevHub with your permissions. Install it only if you trust where it comes from. It's ready after a restart.",
            row.HasUpdate ? "Update" : "Install", [entry.Url], destructive: false));
        if (!ok)
            return;

        row.IsBusy = true;
        var error = await _installer.InstallAsync(entry);
        row.IsBusy = false;
        if (error is not null)
        {
            Message = error;
            return;
        }
        // Switched on, so it loads after the restart (null = everything is on anyway).
        _store.Update(s =>
        {
            if (s.EnabledPlugins is { } enabled && !enabled.Contains(entry.Id))
                enabled.Add(entry.Id);
        });
        Message = $"{entry.Name} {entry.Version} is ready: restart DevHub to use it.";
        Pending();
    }

    private void Remove(MarketplaceRowViewModel row)
    {
        _installer.Remove(row.Id);
        Message = $"{row.Name} is removed when DevHub restarts.";
        Pending();
    }

    private void Pending()
    {
        ChangesPending = true;
        ShowInstalled(Rows.Select(r => r.Offered).OfType<IndexEntry>().ToList());
        _changed();
    }
}

/// <summary>One marketplace plugin: what's offered, what's installed, what's waiting for a restart.</summary>
public sealed partial class MarketplaceRowViewModel : ViewModelBase
{
    private readonly Func<MarketplaceRowViewModel, Task> _install;
    private readonly Action<MarketplaceRowViewModel> _remove;

    public MarketplaceRowViewModel(string id, string name, string? description, IndexEntry? offered, InstalledPlugin? installed,
        Func<MarketplaceRowViewModel, Task> install, Action<MarketplaceRowViewModel> remove)
    {
        Id = id;
        Name = name;
        Description = description ?? "";
        Offered = offered;
        Installed = installed;
        _install = install;
        _remove = remove;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public IndexEntry? Offered { get; }
    public InstalledPlugin? Installed { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(RemoveCommand))]
    private bool _isBusy;

    private bool IsInstalled => Installed is { PendingRemoval: false } && Installed.Info.Version.Length > 0;
    private bool IsWaiting => Installed is { PendingVersion: not null } || Installed is { PendingRemoval: true };

    public bool HasUpdate => IsInstalled && Offered is { IsCompatible: true } offered && Marketplace.IsNewer(offered.Version, Installed!.Info.Version);
    public bool CanInstallNow => Offered is { IsCompatible: true } && !IsWaiting && (!IsInstalled || HasUpdate);
    public bool CanRemoveNow => Installed is { PendingRemoval: false };
    public string InstallText => HasUpdate ? $"Update to {Offered!.Version}" : "Install";

    /// <summary>"Installed 1.2.0 · 1.3.0 after restart", "Needs a newer DevHub"…</summary>
    public string StatusText =>
        Installed is { PendingRemoval: true } ? "Removed after the restart"
        : Installed is { PendingVersion: { } pending } ? (IsInstalled ? $"Installed {Installed.Info.Version} · {pending} after the restart" : $"{pending} after the restart")
        : IsInstalled ? $"Installed {Installed!.Info.Version}" + (Offered is null ? " · no longer offered" : "")
        : Offered is { IsCompatible: false } ? $"{Offered.Version}: made for another DevHub version"
        : Offered is { } offered ? offered.Version
        : "";

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallAsync() => _install(this);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove() => _remove(this);

    private bool CanInstall() => CanInstallNow && !IsBusy;
    private bool CanRemove() => CanRemoveNow && !IsBusy;

    public override string ToString() => $"{Name}, {StatusText}";
}
