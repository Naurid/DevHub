using System.Reflection;
using System.Runtime.Loader;
using DevHub.Sdk.Core.Plugins;
using DevHub.Sdk.Core.Plugins.Marketplace;
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
/// Discovery:
/// <list type="bullet">
/// <item>Built-in plugins: every <c>DevHub*.dll</c> next to the app.</item>
/// <item>Marketplace plugins: one folder each under <see cref="MarketplaceRoot"/>
///       (<c>%AppData%\DevHub\plugins\&lt;id&gt;</c>, with its <c>plugin.json</c>). Pending installs
///       and removals are applied first (<see cref="PluginInstaller.ApplyPending"/>). Their DLLs load
///       into the app's own load context; what they need beyond the app's assemblies (Sdk, Avalonia…)
///       is found in their folder. A plugin made for another plugin API version, or with the id of a
///       built-in plugin, isn't loaded.</item>
/// </list>
/// Each assembly is searched for <see cref="DevHubPluginAttribute"/>.
/// </remarks>
public sealed class PluginHost
{
    private static readonly List<string> MarketplaceFolders = [];

    private PluginHost(IReadOnlyList<IPlugin> available, PluginResolution resolution, IReadOnlyList<string> problems,
        IReadOnlySet<string> marketplaceIds, string marketplaceRoot)
    {
        Available = available;
        // By id: the resolver keeps the first plugin of an id, and so does First.
        Loaded = resolution.Loaded.Select(info => available.First(p => p.Info.Id == info.Id)).ToList();
        Skipped = resolution.Skipped;
        Problems = problems;
        MarketplaceIds = marketplaceIds;
        MarketplaceRoot = marketplaceRoot;
    }

    /// <summary>Every plugin found, in discovery order.</summary>
    public IReadOnlyList<IPlugin> Available { get; }

    /// <summary>The plugins that load this run, requirements first.</summary>
    public IReadOnlyList<IPlugin> Loaded { get; }

    /// <summary>Plugins that were switched on but can't load, with the reason.</summary>
    public IReadOnlyList<SkippedPlugin> Skipped { get; }

    /// <summary>Plugins that couldn't even be loaded or created (a bug in the plugin, a wrong API version…).</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Ids of the plugins installed from the marketplace (the rest are built in).</summary>
    public IReadOnlySet<string> MarketplaceIds { get; }

    /// <summary>Where marketplace plugins are installed.</summary>
    public string MarketplaceRoot { get; }

    public bool IsLoaded(string pluginId) => Loaded.Any(p => p.Info.Id == pluginId);

    /// <summary>Discovers the plugins and resolves which ones load, from the settings' <see cref="AppSettings.EnabledPlugins"/>.</summary>
    /// <param name="marketplaceRoot">Where marketplace plugins are installed (<see cref="PluginInstaller.DefaultRoot"/>).</param>
    public static PluginHost Load(ISettingsStore store, string marketplaceRoot)
    {
        var problems = new List<string>(PluginInstaller.ApplyPending(marketplaceRoot));
        var marketplaceIds = new HashSet<string>(StringComparer.Ordinal);
        var available = Discover(problems, marketplaceRoot, marketplaceIds);
        var resolution = PluginResolver.Resolve(available.Select(p => p.Info), store.Load().EnabledPlugins);
        return new PluginHost(available, resolution, problems, marketplaceIds, marketplaceRoot);
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

    private static List<IPlugin> Discover(List<string> problems, string marketplaceRoot, HashSet<string> marketplaceIds)
    {
        // Built in: the DevHub assemblies next to the app (plugins referenced by the app are copied there).
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "DevHub*.dll"))
        {
            try
            {
                var name = AssemblyName.GetAssemblyName(path);
                if (!IsAssemblyLoaded(name.Name))
                    Assembly.Load(name);
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                problems.Add(Path.GetFileNameWithoutExtension(path));
            }
        }
        var builtIn = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("DevHub", StringComparison.Ordinal) == true)
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
            .ToList();
        var plugins = Create(builtIn, problems);
        var builtInIds = plugins.Select(p => p.Info.Id).ToHashSet(StringComparer.Ordinal);

        // From the marketplace: one folder per plugin.
        foreach (var (folder, info) in MarketplacePlugins(marketplaceRoot, problems))
        {
            if (builtInIds.Contains(info.Id))
            {
                problems.Add($"{info.Name} (the built-in plugin {info.Id} is used instead)");
                continue;
            }
            foreach (var plugin in Create(LoadFolder(folder, problems), problems))
            {
                if (plugins.Any(p => p.Info.Id == plugin.Info.Id))
                    continue;
                plugins.Add(plugin);
                marketplaceIds.Add(plugin.Info.Id);
            }
        }

        // Their own order first (Projects before Git in the menus), then the id: stable on every start.
        return plugins.OrderBy(p => p.Info.Order).ThenBy(p => p.Info.Id, StringComparer.Ordinal).ToList();
    }

    private sealed record InstalledFolder(string Folder, PluginPackageInfo Info);

    private static IEnumerable<InstalledFolder> MarketplacePlugins(string root, List<string> problems)
    {
        if (!Directory.Exists(root))
            yield break;
        // Requirements first (else by folder name): a required plugin's own DLLs load before a plugin that
        // needs it could bring a copy of them (LoadFolder skips a DLL whose name is already loaded).
        var found = new List<InstalledFolder>();
        foreach (var folder in Directory.EnumerateDirectories(root).Where(f => Path.GetFileName(f) != ".pending").Order(StringComparer.OrdinalIgnoreCase))
        {
            if (PluginInstaller.ReadInfo(folder) is { } info)
                found.Add(new InstalledFolder(folder, info));
        }
        foreach (var plugin in PluginResolver.RequirementsFirst(found, f => f.Info.Id, f => f.Info.Requires))
        {
            var info = plugin.Info;
            if (info.ApiVersion != PluginApi.Version)
            {
                problems.Add($"{info.Name} (made for plugin API {info.ApiVersion}; this DevHub has {PluginApi.Version})");
                continue;
            }
            yield return plugin;
        }
    }

    /// <summary>Loads a marketplace plugin's DLLs (those the app doesn't already have) into the app's context.</summary>
    private static List<Assembly> LoadFolder(string folder, List<string> problems)
    {
        lock (MarketplaceFolders)
        {
            if (MarketplaceFolders.Count == 0)
                AssemblyLoadContext.Default.Resolving += ResolveFromMarketplaceFolders; // for the app's lifetime
            MarketplaceFolders.Add(folder);
        }

        var assemblies = new List<Assembly>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.dll"))
        {
            try
            {
                var name = AssemblyName.GetAssemblyName(path);
                if (IsAssemblyLoaded(name.Name))
                    continue; // shared with the app (Sdk, Avalonia…): the app's copy is the one
                assemblies.Add(AssemblyLoadContext.Default.LoadFromAssemblyPath(path));
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                problems.Add(Path.GetFileNameWithoutExtension(path));
            }
        }
        return assemblies;
    }

    /// <summary>A dependency of a marketplace plugin that the app doesn't have: look in the plugin folders.</summary>
    private static Assembly? ResolveFromMarketplaceFolders(AssemblyLoadContext context, AssemblyName name)
    {
        lock (MarketplaceFolders)
        {
            foreach (var folder in MarketplaceFolders)
            {
                var path = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(path))
                    return context.LoadFromAssemblyPath(path);
            }
        }
        return null;
    }

    private static bool IsAssemblyLoaded(string? assemblyName) =>
        AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == assemblyName);

    private static List<IPlugin> Create(IEnumerable<Assembly> assemblies, List<string> problems)
    {
        var plugins = new List<IPlugin>();
        foreach (var assembly in assemblies)
        {
            List<DevHubPluginAttribute> attributes;
            try
            {
                attributes = assembly.GetCustomAttributes<DevHubPluginAttribute>().ToList();
            }
            catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or FileLoadException)
            {
                problems.Add(assembly.GetName().Name ?? "a plugin");
                continue;
            }
            foreach (var attribute in attributes)
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
