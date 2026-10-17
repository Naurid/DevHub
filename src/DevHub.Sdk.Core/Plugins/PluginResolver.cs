using DevHub.Sdk.Core.Localization;

namespace DevHub.Sdk.Core.Plugins;

/// <summary>A plugin that won't be loaded, and why (shown on the Plugins page and in the status bar).</summary>
public sealed record SkippedPlugin(PluginInfo Plugin, string Reason);

/// <summary>The outcome of <see cref="PluginResolver.Resolve"/>.</summary>
/// <param name="Loaded">The plugins to load, dependencies before the plugins that need them.</param>
/// <param name="Skipped">Plugins that were wanted but can't be loaded.</param>
public sealed record PluginResolution(IReadOnlyList<PluginInfo> Loaded, IReadOnlyList<SkippedPlugin> Skipped);

/// <summary>
/// Decides which plugins load, from the plugins found and the ones the user switched on:
/// <list type="bullet">
/// <item>A plugin's requirements are switched on with it (turning on GitHub turns on Git).</item>
/// <item>A plugin whose requirement isn't installed is skipped, and so is everything that needs it.</item>
/// <item>Plugins that require each other in a circle are skipped (none of them can go first).</item>
/// <item>A second plugin with an id that's already taken is skipped.</item>
/// <item>The load order puts every plugin after its requirements; otherwise it keeps the order
///       the plugins were found in, so it's the same on every start.</item>
/// </list>
/// Ids the user switched on but that aren't installed are simply ignored (and stay in settings).
/// </summary>
public static class PluginResolver
{
    /// <param name="available">Every plugin found, in discovery order.</param>
    /// <param name="enabled">Ids the user switched on; null means all of them (before the first choice).</param>
    public static PluginResolution Resolve(IEnumerable<PluginInfo> available, IReadOnlyCollection<string>? enabled)
    {
        var skipped = new List<SkippedPlugin>();

        // One plugin per id; the first one found wins.
        var byId = new Dictionary<string, PluginInfo>(StringComparer.Ordinal);
        var order = new List<PluginInfo>();
        foreach (var plugin in available)
        {
            if (byId.TryAdd(plugin.Id, plugin))
                order.Add(plugin);
            else
                skipped.Add(new SkippedPlugin(plugin, L.T($"Another plugin already uses the id \"{plugin.Id}\".")));
        }

        // What the user asked for, plus everything it requires (as far as it's installed).
        var wanted = new HashSet<string>(enabled is null ? byId.Keys : enabled.Where(byId.ContainsKey), StringComparer.Ordinal);
        var queue = new Queue<string>(wanted);
        while (queue.Count > 0)
        {
            foreach (var requirement in byId[queue.Dequeue()].Requires)
            {
                if (byId.ContainsKey(requirement) && wanted.Add(requirement))
                    queue.Enqueue(requirement);
            }
        }

        // Missing requirements, and everything that (indirectly) depends on a skipped plugin.
        var candidates = order.Where(p => wanted.Contains(p.Id)).ToList();
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var plugin in candidates.Where(p => !dropped.Contains(p.Id)))
            {
                var reason = plugin.Requires.FirstOrDefault(r => !byId.ContainsKey(r)) is { } missing
                    ? L.T($"Requires \"{missing}\", which isn't installed.")
                    : plugin.Requires.FirstOrDefault(dropped.Contains) is { } failed
                        ? L.T($"Requires {byId[failed].Name}, which couldn't be loaded.")
                        : null;
                if (reason is null)
                    continue;
                dropped.Add(plugin.Id);
                skipped.Add(new SkippedPlugin(plugin, reason));
                changed = true;
            }
        } while (changed);

        // Dependency order: repeatedly take the first plugin (in discovery order) whose
        // requirements are all placed. What's left at the end requires itself in a circle.
        var remaining = candidates.Where(p => !dropped.Contains(p.Id)).ToList();
        var loaded = new List<PluginInfo>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        while (remaining.FirstOrDefault(p => p.Requires.All(placed.Contains)) is { } next)
        {
            loaded.Add(next);
            placed.Add(next.Id);
            remaining.Remove(next);
        }
        foreach (var plugin in remaining)
            skipped.Add(new SkippedPlugin(plugin, L.T("Its requirements form a circle (") + string.Join(", ", remaining.Select(p => p.Id)) + ")."));

        return new PluginResolution(loaded, skipped);
    }

    /// <summary>
    /// Puts every item after the items it requires, otherwise keeping the given order; used to load marketplace
    /// plugin folders requirements first, so a required plugin's own DLLs are the ones that load (a later folder's
    /// copy of a DLL with the same name is skipped). Requirements that aren't in the list don't hold anything back;
    /// items that require each other in a circle come last, in their given order. Ids compare without case.
    /// </summary>
    public static IReadOnlyList<T> RequirementsFirst<T>(IEnumerable<T> items, Func<T, string> id, Func<T, IEnumerable<string>?> requires)
        where T : class
    {
        var remaining = items.ToList();
        var listed = remaining.Select(id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<T>();
        while (remaining.FirstOrDefault(item => (requires(item) ?? []).All(r => placed.Contains(r) || !listed.Contains(r))) is { } next
               && remaining.Remove(next))
        {
            ordered.Add(next);
            placed.Add(id(next));
        }
        ordered.AddRange(remaining);
        return ordered;
    }

    /// <summary>
    /// The plugins that need <paramref name="pluginId"/>, directly or indirectly: what stops working
    /// when it's switched off (the Plugins page warns about these).
    /// </summary>
    public static IReadOnlyList<PluginInfo> Dependents(IEnumerable<PluginInfo> plugins, string pluginId)
    {
        var all = plugins.ToList();
        var found = new List<PluginInfo>();
        var ids = new HashSet<string>(StringComparer.Ordinal) { pluginId };
        bool changed;
        do
        {
            changed = false;
            foreach (var plugin in all.Where(p => !ids.Contains(p.Id) && p.Requires.Any(ids.Contains)))
            {
                ids.Add(plugin.Id);
                found.Add(plugin);
                changed = true;
            }
        } while (changed);
        return found;
    }
}
