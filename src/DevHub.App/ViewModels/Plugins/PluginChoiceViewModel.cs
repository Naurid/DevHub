using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Plugins;
using DevHub.Sdk.Core.Plugins;
using DevHub.Sdk.Core.Settings;
using DevHub.Sdk.Core.Localization;

namespace DevHub.ViewModels.Plugins;

/// <summary>
/// The plugin checklist shared by Settings → Plugins and the first-run picker: one row per
/// installed plugin, presets, and a note when a click switched other plugins on or off too
/// (the rules live in <see cref="PluginSelection"/>). Nothing is saved here; the owner saves
/// <see cref="Enabled"/> with <see cref="Save"/>.
/// </summary>
public sealed partial class PluginChoiceViewModel : ViewModelBase
{
    private readonly PluginSelection _selection;
    private readonly IReadOnlySet<string> _loaded;
    private bool _syncing;

    /// <param name="host">What's installed and what loaded this run.</param>
    /// <param name="enabled">The ids switched on to start from.</param>
    public PluginChoiceViewModel(PluginHost host, IEnumerable<string> enabled)
    {
        var available = host.Available.Select(p => p.Info).OrderBy(p => p.Order).ToList();
        _selection = new PluginSelection(available, enabled);
        _loaded = host.Loaded.Select(p => p.Info.Id).ToHashSet(StringComparer.Ordinal);
        var skipped = host.Skipped.ToDictionary(s => s.Plugin.Id, s => s.Reason, StringComparer.Ordinal);

        string? lastCategory = null;
        Rows = available.Select(info =>
        {
            var header = info.Category != lastCategory ? info.Category : null;
            lastCategory = info.Category;
            var needs = info.Requires.Select(id => available.FirstOrDefault(p => p.Id == id)?.Name ?? id).ToList();
            return new PluginRowViewModel(info, header, needs.Count > 0 ? L.T("Needs ") + string.Join(", ", needs) : null,
                _loaded.Contains(info.Id), skipped.GetValueOrDefault(info.Id), _selection.IsEnabled(info.Id), OnRowToggled);
        }).ToList();
        Presets = _selection.Presets;
    }

    public IReadOnlyList<PluginRowViewModel> Rows { get; }

    /// <summary>"Git client", "Project templates", …, "Everything".</summary>
    public IReadOnlyList<string> Presets { get; }

    /// <summary>The presets as buttons: the name shown (translated) and the one applied.</summary>
    public IReadOnlyList<NamedPreset> PresetButtons => [.. Presets.Select(p => new NamedPreset(p, L.T(p)))];

    /// <summary>The preset the current choice matches exactly, or null (a custom choice).</summary>
    public string? MatchingPreset => _selection.MatchingPreset;

    /// <summary>"Preset: Git client", or "Your own choice".</summary>
    public string MatchingPresetText => MatchingPreset is { } p ? L.T($"Preset: {L.T(p)}") : L.T("Your own choice");

    /// <summary>What a click also changed ("GitHub was switched off too: it needs Git."), or null.</summary>
    [ObservableProperty] private string? _note;

    /// <summary>The switched-on ids, in plugin order.</summary>
    public IReadOnlyList<string> Enabled => _selection.Enabled;

    /// <summary>The choice differs from what's loaded now, so it takes effect after a restart.</summary>
    public bool RestartNeeded => !_loaded.SetEquals(_selection.Enabled);

    /// <summary>Raised after every change (a click or a preset).</summary>
    public event EventHandler? Changed;

    [RelayCommand]
    private void ApplyPreset(string preset)
    {
        _selection.ApplyPreset(preset);
        Note = null;
        Sync();
    }

    private void OnRowToggled(PluginRowViewModel row, bool isEnabled)
    {
        if (_syncing)
            return;
        if (isEnabled)
        {
            var also = _selection.Enable(row.Info.Id);
            Note = also.Count == 0 ? null
                : also.Count == 1 ? L.T($"{Names(also)} switched on too: {row.Info.Name} needs it.")
                : L.T($"{Names(also)} switched on too: {row.Info.Name} needs them.");
        }
        else
        {
            var also = _selection.Disable(row.Info.Id);
            Note = also.Count == 0 ? null
                : also.Count == 1 ? L.T($"{Names(also)} switched off too: it needs {row.Info.Name}.")
                : L.T($"{Names(also)} switched off too: they need {row.Info.Name}.");
        }
        Sync();
    }

    /// <summary>Puts the selection back into the rows (a click can change several), then tells the owner.</summary>
    private void Sync()
    {
        _syncing = true;
        foreach (var row in Rows)
            row.IsEnabled = _selection.IsEnabled(row.Info.Id);
        _syncing = false;
        OnPropertyChanged(nameof(MatchingPreset));
        OnPropertyChanged(nameof(MatchingPresetText));
        OnPropertyChanged(nameof(RestartNeeded));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Names(IReadOnlyList<PluginInfo> plugins) => string.Join(", ", plugins.Select(p => p.Name));

    /// <summary>
    /// Saves the choice as <see cref="AppSettings.EnabledPlugins"/>. Ids of plugins that aren't
    /// installed right now (saved by an earlier setup) are kept, so they come back if reinstalled.
    /// </summary>
    public void Save(ISettingsStore store, PluginHost host)
    {
        var installed = host.Available.Select(p => p.Info.Id).ToHashSet(StringComparer.Ordinal);
        var notInstalled = store.Load().EnabledPlugins?.Where(id => !installed.Contains(id)) ?? [];
        var enabled = Enabled.Concat(notInstalled).ToList();
        store.Update(s => s.EnabledPlugins = enabled);
    }
}

/// <summary>One plugin in the checklist: switch, name, description, what it needs, its state this run.</summary>
public sealed partial class PluginRowViewModel : ObservableObject
{
    private readonly Action<PluginRowViewModel, bool> _toggled;

    public PluginRowViewModel(PluginInfo info, string? categoryHeader, string? requiresText, bool isLoaded,
        string? skipReason, bool isEnabled, Action<PluginRowViewModel, bool> toggled)
    {
        Info = info;
        CategoryHeader = categoryHeader is null ? null : L.T(categoryHeader);
        RequiresText = requiresText;
        IsLoaded = isLoaded;
        SkipReason = skipReason;
        _isEnabled = isEnabled;
        _toggled = toggled;
    }

    public PluginInfo Info { get; }
    public string Name => L.T(Info.Name);   // a plugin may ship the French name in its i18n file
    public string Description => L.T(Info.Description);

    /// <summary>The category, on the first row of each category (a small header above it); else null.</summary>
    public string? CategoryHeader { get; }

    /// <summary>"Needs Git", or null.</summary>
    public string? RequiresText { get; }

    public bool IsLoaded { get; }

    /// <summary>Why it was switched on but couldn't load this run, or null.</summary>
    public string? SkipReason { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(HasProblem))]
    private bool _isEnabled;

    partial void OnIsEnabledChanged(bool value) => _toggled(this, value);

    /// <summary>"On", "Off", "On after a restart", "Off after a restart", or why it couldn't load.</summary>
    public string StatusText => (IsEnabled, IsLoaded) switch
    {
        (true, true) => L.T("On"),
        (false, false) => L.T("Off"),
        (true, false) => SkipReason is { } reason ? L.T("Couldn't load: ") + reason : L.T("On after a restart"),
        (false, true) => L.T("Off after a restart"),
    };

    public bool HasProblem => IsEnabled && !IsLoaded && SkipReason is not null;

    public override string ToString() => Name;
}

/// <summary>A preset button: <see cref="Id"/> is the preset's (English) name, <see cref="Text"/> what the button shows.</summary>
public sealed record NamedPreset(string Id, string Text)
{
    public override string ToString() => Text;
}
