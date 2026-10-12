namespace DevHub.Sdk.Core.Plugins.Marketplace;

/// <summary>What installing a plugin takes: the packages to install, requirements first, or why it can't be done.</summary>
/// <param name="Install">The plugin's missing requirements (each after its own), then the plugin itself. Empty when there's a problem.</param>
/// <param name="Problem">Why it can't be installed (nothing is), or null.</param>
public sealed record InstallPlan(IReadOnlyList<IndexEntry> Install, string? Problem)
{
    /// <summary>The requirements that come along (everything but the plugin itself).</summary>
    public IReadOnlyList<IndexEntry> AlsoInstalled => Install.Count > 1 ? Install.Take(Install.Count - 1).ToList() : [];
}

/// <summary>
/// Dependencies between marketplace plugins (a plugin.json's <c>requires</c>), for installing and removing:
/// <list type="bullet">
/// <item>Installing a plugin installs what it requires and isn't there yet (built in or installed),
///       from the marketplace, requirements of requirements included, each before the plugins that need it.</item>
/// <item>A requirement no source offers, one made for another plugin API, or plugins requiring each other in a
///       circle stop the install before anything is downloaded.</item>
/// <item>Removing a plugin removes the installed plugins that need it (directly or through another one), after asking.</item>
/// </list>
/// Plugin ids compare without case, like the marketplace's. Requirements have no versions: any installed copy counts.
/// </summary>
public static class InstallPlanner
{
    /// <param name="plugin">The plugin to install (or update).</param>
    /// <param name="offered">Everything the marketplace sources offer.</param>
    /// <param name="present">Ids of the plugins already there: built in, installed, or waiting to be installed at the
    ///                       next start (not those waiting to be removed).</param>
    public static InstallPlan PlanInstall(IndexEntry plugin, IReadOnlyList<IndexEntry> offered, IEnumerable<string> present)
    {
        if (!plugin.IsCompatible)
            return Fail($"{plugin.Name} is made for plugin API {plugin.ApiVersion}; this DevHub has {PluginApi.Version}. Update DevHub (or wait for a new version of the plugin).");

        var there = new HashSet<string>(present, StringComparer.OrdinalIgnoreCase);
        there.Remove(plugin.Id); // the plugin itself is always (re)installed: an update
        var plan = new List<IndexEntry>();
        var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new List<IndexEntry>(); // the chain being followed, to spot a circle

        // Depth first: a plugin goes into the plan after everything it requires.
        string? Visit(IndexEntry entry)
        {
            if (path.Any(p => p.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var circle = path.SkipWhile(p => !p.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).Append(entry.Name);
                return $"{plugin.Name} can't be installed: its requirements need each other in a circle ({string.Join(" → ", circle)}).";
            }
            path.Add(entry);
            foreach (var id in entry.Requires ?? [])
            {
                if (there.Contains(id) || planned.Contains(id))
                    continue;
                var requirement = offered.FirstOrDefault(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (requirement is null)
                    return $"{entry.Name} requires \"{id}\", which no marketplace source offers. Nothing was installed.";
                if (!requirement.IsCompatible)
                    return $"{entry.Name} requires {requirement.Name}, which is made for plugin API {requirement.ApiVersion}; this DevHub has {PluginApi.Version}. Nothing was installed.";
                if (Visit(requirement) is { } problem)
                    return problem;
            }
            path.RemoveAt(path.Count - 1);
            if (planned.Add(entry.Id))
                plan.Add(entry);
            return null;
        }

        return Visit(plugin) is { } failure ? Fail(failure) : new InstallPlan(plan, null);
    }

    /// <summary>
    /// The installed plugins that stop working when <paramref name="id"/> is removed: those that require it, and
    /// those that require one of them, and so on (in the order <paramref name="installed"/> lists them).
    /// </summary>
    /// <param name="installed">The installed marketplace plugins (those waiting to be installed included, those
    ///                         waiting to be removed left out).</param>
    public static IReadOnlyList<PluginPackageInfo> Dependents(string id, IReadOnlyList<PluginPackageInfo> installed)
    {
        var gone = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        bool changed;
        do
        {
            changed = false;
            foreach (var plugin in installed)
            {
                if (!gone.Contains(plugin.Id) && (plugin.Requires ?? []).Any(gone.Contains))
                    changed = gone.Add(plugin.Id) | changed;
            }
        } while (changed);
        return installed.Where(p => !p.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && gone.Contains(p.Id)).ToList();
    }

    private static InstallPlan Fail(string problem) => new([], problem);
}
