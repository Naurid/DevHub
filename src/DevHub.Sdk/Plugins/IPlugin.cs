using DevHub.Sdk.Core.Plugins;
using DevHub.Sdk.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DevHub.Plugins;

/// <summary>
/// A DevHub plugin: one feature (Projects, Git, GitHub, Templates, …) that the user can switch on
/// or off. The app finds it through <see cref="DevHubPluginAttribute"/> on its assembly, and, if
/// it's switched on (and its requirements can load), calls <see cref="ConfigureServices"/> once
/// at startup before the DI container is built. A switched-off plugin isn't touched at all.
/// </summary>
/// <remarks>
/// A plugin contributes only through DI, into the app's extension points:
/// <list type="bullet">
/// <item><c>IActionProvider</c>: actions (menus via <c>AppAction.Menu</c>, toolbar, Welcome, shortcuts).</item>
/// <item><c>DocumentViewModelBase</c> (single tabs) and <c>IDocumentFactory</c> (one tab per id).</item>
/// <item><c>ToolWindowViewModelBase</c>: docked panels.</item>
/// <item><c>SettingsPageViewModelBase</c> (transient): pages of the Settings dialog.</item>
/// <item><c>TitleBarWidgetViewModelBase</c> / <c>StatusBarWidgetViewModelBase</c>: widgets.</item>
/// <item>Its own services, for itself and for plugins that require it.</item>
/// </list>
/// Settings go in its own section (<see cref="ISettingsSection"/>, keyed by <c>Info.Id</c>).
/// </remarks>
public interface IPlugin
{
    PluginInfo Info { get; }

    /// <summary>Registers the plugin's services and contributions.</summary>
    void ConfigureServices(IServiceCollection services, PluginContext context);

    /// <summary>
    /// Resource dictionaries (e.g. the plugin's own design tokens) merged into the app's resources
    /// before any window opens: <c>avares://DevHub.Git/Themes/GitTokens.axaml</c>.
    /// </summary>
    IEnumerable<Uri> Resources => [];
}

/// <summary>What the host hands a plugin while it registers its services.</summary>
/// <param name="Settings">The settings store, e.g. to read the plugin's section during registration.</param>
/// <param name="LoadedPlugins">Ids of every plugin that loads this run (for optional integrations).</param>
public sealed record PluginContext(ISettingsStore Settings, IReadOnlySet<string> LoadedPlugins);

/// <summary>
/// Marks an assembly as containing a plugin: <c>[assembly: DevHubPlugin(typeof(GitPlugin))]</c>.
/// The type needs a public parameterless constructor. An assembly may hold several plugins.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DevHubPluginAttribute(Type pluginType) : Attribute
{
    public Type PluginType { get; } = pluginType;
}
