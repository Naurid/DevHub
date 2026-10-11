namespace DevHub.Sdk.Core.Plugins;

/// <summary>
/// Which plugins the user has switched on, edited as they click in the Plugins page or the
/// first-run picker. Keeps the choice consistent: switching a plugin on also switches on what it
/// requires; switching one off also switches off what requires it. Each change returns the other
/// plugins it touched, so the UI can say so ("GitHub was switched off too: it needs Git").
/// </summary>
public sealed class PluginSelection
{
    /// <summary>The preset that switches on every plugin.</summary>
    public const string Everything = "Everything";

    private readonly IReadOnlyList<PluginInfo> _available;
    private readonly HashSet<string> _enabled;

    /// <param name="available">Every installed plugin.</param>
    /// <param name="enabled">The ids switched on so far (ids of plugins that aren't installed are ignored).</param>
    public PluginSelection(IReadOnlyList<PluginInfo> available, IEnumerable<string> enabled)
    {
        _available = available;
        _enabled = enabled.Where(id => available.Any(p => p.Id == id)).ToHashSet(StringComparer.Ordinal);
    }

    public bool IsEnabled(string id) => _enabled.Contains(id);

    /// <summary>The switched-on ids, in the plugins' order.</summary>
    public IReadOnlyList<string> Enabled => _available.Where(p => _enabled.Contains(p.Id)).Select(p => p.Id).ToList();

    /// <summary>The presets the plugins name, in plugin order, then <see cref="Everything"/>.</summary>
    public IReadOnlyList<string> Presets =>
        _available.OrderBy(p => p.Order).SelectMany(p => p.Presets).Distinct(StringComparer.Ordinal).Append(Everything).ToList();

    /// <summary>Switches a plugin on, with everything it requires. Returns the requirements switched on along with it.</summary>
    public IReadOnlyList<PluginInfo> Enable(string id)
    {
        var also = new List<PluginInfo>();
        var queue = new Queue<string>([id]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (_available.FirstOrDefault(p => p.Id == current) is not { } plugin || !_enabled.Add(current))
                continue;
            if (current != id)
                also.Add(plugin);
            foreach (var requirement in plugin.Requires)
                queue.Enqueue(requirement);
        }
        return also;
    }

    /// <summary>Switches a plugin off, with everything that requires it. Returns the dependents switched off along with it.</summary>
    public IReadOnlyList<PluginInfo> Disable(string id)
    {
        if (!_enabled.Remove(id))
            return [];
        var also = PluginResolver.Dependents(_available, id).Where(p => _enabled.Remove(p.Id)).ToList();
        return also;
    }

    /// <summary>Switches on exactly the preset's plugins (and their requirements); <see cref="Everything"/> = all.</summary>
    public void ApplyPreset(string preset)
    {
        _enabled.Clear();
        foreach (var plugin in _available.Where(p => preset == Everything || p.Presets.Contains(preset)))
            Enable(plugin.Id);
    }

    /// <summary>The preset that matches the current choice exactly, or null.</summary>
    public string? MatchingPreset =>
        Presets.FirstOrDefault(preset =>
        {
            var test = new PluginSelection(_available, []);
            test.ApplyPreset(preset);
            return test._enabled.SetEquals(_enabled);
        });
}
