namespace DevHub.Sdk.Core.Plugins.Marketplace;

/// <summary>A plugin folder: its plugin.json, and whether it came with DevHub (bundled) or was installed.</summary>
public sealed record PluginFolder(string Folder, PluginPackageInfo Info, bool Bundled);

/// <summary>Which folders load (<see cref="PluginFolders.Choose"/>), and the installed copies that lost to a bundled one.</summary>
/// <param name="Chosen">One folder per plugin id.</param>
/// <param name="Outdated">Installed copies that are not newer than the bundled one (left alone; never loaded).</param>
public sealed record PluginFolderChoice(IReadOnlyList<PluginFolder> Chosen, IReadOnlyList<PluginFolder> Outdated);

/// <summary>
/// Plugins that live in folders (<c>&lt;id&gt;\plugin.json</c> + DLLs), in two places:
/// <list type="bullet">
/// <item><b>Bundled</b>: <c>plugins\</c> next to DevHub's executable, installed and updated with DevHub (read-only for it).</item>
/// <item><b>Installed</b>: <c>%AppData%\DevHub\plugins</c> (<see cref="PluginInstaller"/>), from the marketplace.</item>
/// </list>
/// When both have a plugin, the newer version loads: an update from the marketplace wins over the bundled copy,
/// until a DevHub update bundles a version at least as new. Only the chosen folder's DLLs are ever loaded.
/// </summary>
public static class PluginFolders
{
    /// <summary>The bundled plugins' folder: <c>plugins</c> next to the executable.</summary>
    public static string BundledRoot(string appDirectory) => Path.Combine(appDirectory, "plugins");

    /// <summary>The plugin folders directly under <paramref name="root"/> that have a readable plugin.json (by folder name).</summary>
    public static IReadOnlyList<PluginFolder> Scan(string root, bool bundled)
    {
        if (!Directory.Exists(root))
            return [];
        var found = new List<PluginFolder>();
        foreach (var folder in Directory.EnumerateDirectories(root).Where(f => Path.GetFileName(f) != ".pending").Order(StringComparer.OrdinalIgnoreCase))
        {
            if (PluginInstaller.ReadInfo(folder) is { } info && !string.IsNullOrWhiteSpace(info.Id))
                found.Add(new PluginFolder(folder, info, bundled));
        }
        return found;
    }

    /// <summary>
    /// One folder per plugin id: an installed copy wins when its version is newer than the bundled one; otherwise
    /// the bundled copy (also on a tie). Ids compare without case. Bundled folders keep their order, then installed ones.
    /// </summary>
    public static PluginFolderChoice Choose(IEnumerable<PluginFolder> bundled, IEnumerable<PluginFolder> installed)
    {
        var chosen = new List<PluginFolder>();
        foreach (var folder in bundled)
        {
            if (chosen.All(c => !c.Info.Id.Equals(folder.Info.Id, StringComparison.OrdinalIgnoreCase)))
                chosen.Add(folder);
        }
        var outdated = new List<PluginFolder>();
        foreach (var folder in installed)
        {
            var index = chosen.FindIndex(c => c.Info.Id.Equals(folder.Info.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                chosen.Add(folder);
            else if (chosen[index].Bundled && Marketplace.IsNewer(folder.Info.Version, chosen[index].Info.Version))
                chosen[index] = folder; // an update of the bundled plugin
            else if (chosen[index].Bundled)
                outdated.Add(folder);
        }
        return new PluginFolderChoice(chosen, outdated);
    }
}
