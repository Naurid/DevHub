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

    /// <summary>The environment variable listing plugin folders to load while developing them (<see cref="ScanPaths"/>).</summary>
    public const string DevelopmentPathsVariable = "DEVHUB_PLUGIN_PATHS";

    /// <summary>"C:\a\bin\Debug\net10.0;C:\b\out" → the paths (separated by ';' on Windows, ':' elsewhere; blanks dropped).</summary>
    public static IReadOnlyList<string> ParsePaths(string? value) =>
        (value ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Plugin folders to develop with: each path is a plugin folder (a plugin.json in it, e.g. a plugin's build
    /// output), or a folder whose subfolders are. Relative paths are relative to the current directory. A path
    /// that doesn't exist or holds no plugin is reported in <paramref name="problems"/>.
    /// </summary>
    public static IReadOnlyList<PluginFolder> ScanPaths(IEnumerable<string> paths, List<string> problems)
    {
        var found = new List<PluginFolder>();
        foreach (var path in paths.Select(Path.GetFullPath))
        {
            if (!Directory.Exists(path))
            {
                problems.Add($"Plugin path not found: {path}");
                continue;
            }
            var here = PluginInstaller.ReadInfo(path) is { } info && !string.IsNullOrWhiteSpace(info.Id)
                ? [new PluginFolder(path, info, Bundled: false)]
                : Scan(path, bundled: false);
            if (here.Count == 0)
                problems.Add($"No plugin.json in {path} (nor in its folders)");
            found.AddRange(here);
        }
        return found;
    }

    /// <summary>
    /// Development folders (<see cref="ScanPaths"/>) win over every other copy of their plugin, whatever the
    /// versions (a build output has none): they replace it, in its place; the others follow. The first one of an id counts.
    /// </summary>
    public static IReadOnlyList<PluginFolder> WithDevelopment(IReadOnlyList<PluginFolder> chosen, IReadOnlyList<PluginFolder> development)
    {
        var result = chosen.ToList();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in development.Where(f => taken.Add(f.Info.Id)))
        {
            var index = result.FindIndex(c => c.Info.Id.Equals(folder.Info.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                result[index] = folder;
            else
                result.Add(folder);
        }
        return result;
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
