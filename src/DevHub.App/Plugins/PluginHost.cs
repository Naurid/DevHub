using System.Reflection;
using DevHub.Sdk.Core.Plugins;
using DevHub.Sdk.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DevHub.Plugins;

/// <summary>
/// Finds the plugins, decides which load (<see cref="PluginResolver"/>: the ones switched on in
/// settings plus their requirements) and lets each loaded one register its services. Runs once
/// at startup, before the DI container is built, so a switched-off plugin is never touched;
/// switching plugins takes a restart. Kept in DI afterwards for the Plugins page.
/// </summary>
/// <remarks>
/// Discovery: every <c>DevHub*.dll</c> next to the app is loaded and searched for
/// <see cref="DevHubPluginAttribute"/>. (Later the same scan can cover a user plugins folder.)
/// </remarks>
public sealed class PluginHost
{
    private PluginHost(IReadOnlyList<IPlugin> available, PluginResolution resolution, IReadOnlyList<string> problems)
    {
        Available = available;
        // By id: the resolver keeps the first plugin of an id, and so does First.
        Loaded = resolution.Loaded.Select(info => available.First(p => p.Info.Id == info.Id)).ToList();
        Skipped = resolution.Skipped;
        Problems = problems;
    }

    /// <summary>Every plugin found, in discovery order.</summary>
    public IReadOnlyList<IPlugin> Available { get; }

    /// <summary>The plugins that load this run, requirements first.</summary>
    public IReadOnlyList<IPlugin> Loaded { get; }

    /// <summary>Plugins that were switched on but can't load, with the reason.</summary>
    public IReadOnlyList<SkippedPlugin> Skipped { get; }

    /// <summary>Plugin types that couldn't even be created (a bug in the plugin).</summary>
    public IReadOnlyList<string> Problems { get; }

    public bool IsLoaded(string pluginId) => Loaded.Any(p => p.Info.Id == pluginId);

    /// <summary>Discovers the plugins and resolves which ones load, from the settings' <see cref="AppSettings.EnabledPlugins"/>.</summary>
    public static PluginHost Load(ISettingsStore store)
    {
        var problems = new List<string>();
        var available = Discover(problems);
        var resolution = PluginResolver.Resolve(available.Select(p => p.Info), store.Load().EnabledPlugins);
        return new PluginHost(available, resolution, problems);
    }

    /// <summary>Lets every loaded plugin register its services, in dependency order.</summary>
    public void ConfigureServices(IServiceCollection services, ISettingsStore store)
    {
        var context = new PluginContext(store, Loaded.Select(p => p.Info.Id).ToHashSet(StringComparer.Ordinal));
        foreach (var plugin in Loaded)
            plugin.ConfigureServices(services, context);
    }

    /// <summary>The loaded plugins' resource dictionaries, to merge into the app's resources.</summary>
    public IEnumerable<Uri> Resources => Loaded.SelectMany(p => p.Resources);

    /// <summary>One line for the status bar when something didn't load, else null.</summary>
    public string? Summary()
    {
        var names = Skipped.Select(s => s.Plugin.Name).Concat(Problems).ToList();
        return names.Count switch
        {
            0 => null,
            1 => $"The plugin {names[0]} couldn't be loaded: {Skipped.FirstOrDefault()?.Reason ?? "see Settings → Plugins."}",
            _ => $"{names.Count} plugins couldn't be loaded ({string.Join(", ", names)}).",
        };
    }

    private static List<IPlugin> Discover(List<string> problems)
    {
        // Load the DevHub assemblies next to the app (plugins referenced by the app are copied
        // there), then read their [DevHubPlugin] attributes. Sorted by name so the order (and so
        // the load order of unrelated plugins) is the same on every start.
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "DevHub*.dll"))
        {
            try
            {
                var name = AssemblyName.GetAssemblyName(path);
                if (AppDomain.CurrentDomain.GetAssemblies().All(a => a.GetName().Name != name.Name))
                    Assembly.Load(name);
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                problems.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        var plugins = new List<IPlugin>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("DevHub", StringComparison.Ordinal) == true)
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            foreach (var attribute in assembly.GetCustomAttributes<DevHubPluginAttribute>())
            {
                try
                {
                    plugins.Add((IPlugin)Activator.CreateInstance(attribute.PluginType)!);
                }
                catch (Exception ex) when (ex is InvalidCastException or MissingMethodException or TargetInvocationException)
                {
                    problems.Add(attribute.PluginType.Name);
                }
            }
        }
        return plugins;
    }
}
