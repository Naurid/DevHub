using DevHub.Plugins;
using DevHub.Sdk.Core.Plugins;
using DevHub.Sdk.Core.Settings;
using DevHub.ViewModels.Dialogs;

namespace DevHub.ViewModels.Plugins;

/// <summary>
/// First start (no plugin choice saved yet): "what do you need?" Presets and the plugin
/// checklist, starting from Everything. OK saves the choice; the caller restarts DevHub when the
/// choice differs from what's loaded (everything, on a first start).
/// </summary>
public sealed class FirstRunViewModel : DialogViewModelBase
{
    private readonly PluginHost _host;
    private readonly ISettingsStore _store;

    public FirstRunViewModel(PluginHost host, ISettingsStore store)
    {
        _host = host;
        _store = store;
        Choice = new PluginChoiceViewModel(host, host.Available.Select(p => p.Info.Id));
        Choice.ApplyPresetCommand.Execute(PluginSelection.Everything);
    }

    public override string Title => "Welcome to DevHub";
    public override string OkText => "Start DevHub";
    public override bool ShowCancel => false;
    public override double Width => 640;
    public override double Height => 600;

    public PluginChoiceViewModel Choice { get; }

    protected override Task<bool> ApplyChangesAsync()
    {
        Choice.Save(_store, _host);
        return Task.FromResult(true);
    }
}
