using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Plugins;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.ViewModels.Plugins;

namespace DevHub.ViewModels.Dialogs.Settings;

/// <summary>
/// Settings → Plugins: switch features on and off. Plugins load only at startup, so a change
/// takes effect after a restart; the page says which ones change and offers Restart Now.
/// </summary>
public sealed partial class PluginsSettingsPageViewModel : SettingsPageViewModelBase
{
    private readonly PluginHost _host;
    private readonly ISettingsStore _store;
    private readonly IStatusService _status;
    private readonly IAppRestarter _restarter;

    public PluginsSettingsPageViewModel(PluginHost host, ISettingsStore store, IStatusService status, IAppRestarter restarter)
    {
        _host = host;
        _store = store;
        _status = status;
        _restarter = restarter;
        // Never chosen yet: everything installed is on (that's what loads).
        Choice = new PluginChoiceViewModel(host, store.Load().EnabledPlugins ?? host.Available.Select(p => p.Info.Id));
        // Owned by this page (same lifetime): no unsubscribe needed.
        Choice.Changed += (_, _) =>
        {
            MarkModified();
            OnPropertyChanged(nameof(RestartNeeded));
        };
    }

    public override string Title => "Plugins";
    public override int Order => 900;
    public override string Keywords => "plugins features extensions enable disable switch on off projects git github templates restart";

    public PluginChoiceViewModel Choice { get; }

    /// <summary>The choice (saved or not) differs from what's loaded: show the restart banner.</summary>
    public bool RestartNeeded => Choice.RestartNeeded;

    public override void Apply()
    {
        Choice.Save(_store, _host);
        IsModified = false;
        if (RestartNeeded)
            _status.Show("Plugin changes apply when DevHub restarts (File → Restart DevHub).");
    }

    /// <summary>Saves the choice (if not yet) and restarts DevHub with it.</summary>
    [RelayCommand]
    private void RestartNow()
    {
        if (IsModified)
            Apply();
        _restarter.Restart();
    }
}
