# DevHub Sdk

The Sdk is what every DevHub plugin builds on. It ships as two NuGet packages, built from this repository and
released with DevHub (`build/Pack-Sdk.ps1`, `.github/workflows/release.yml`):

| Package | Project | What's in it | Avalonia? |
|---|---|---|---|
| **DevHub.Sdk.Core** | `src/DevHub.Sdk.Core` | Settings store, plugin model and resolver, marketplace, localization, fuzzy search, Markdown, chart and table data, people, list moves | no |
| **DevHub.Sdk** | `src/DevHub.Sdk` | `IPlugin`, ViewModel base classes (documents, tool windows, dialogs, settings pages, widgets), actions, shell services, integration hooks between plugins, controls, charts, design tokens, icons | yes (Avalonia 12) |

A plugin's `.Core` project references **DevHub.Sdk.Core** only; its UI project references **DevHub.Sdk** (which
brings Sdk.Core along). Neither package ever references the shell (`DevHub.App`) or a plugin.

## The reference

- [SDK_CORE.md](SDK_CORE.md): `DevHub.Sdk.Core`, namespace by namespace.
- [SDK.md](SDK.md): `DevHub.Sdk`, namespace by namespace.

Both list the public types a plugin uses, what each one is for and the rules that come with it. The XML comments
ship in the packages too, so IntelliSense shows the same text on every member.

## The guides (how to put the pieces together)

| Topic | Where |
|---|---|
| How plugins load, the contract, extension points, settings, linking plugins, a recipe for a new plugin, the marketplace | [../PLUGINS.md](../PLUGINS.md) |
| UI rules, tokens, components, recipes (an action, a document, a tool window, a dialog, a settings page, a widget, translations) | [../UI_GUIDE.md](../UI_GUIDE.md) |
| Actions, menus, shortcuts and Find Action in depth | [../ACTIONS_AND_FUZZY_MATCHING.md](../ACTIONS_AND_FUZZY_MATCHING.md) |
| Versions, releases, the Sdk packages on nuget.org | [../RELEASING.md](../RELEASING.md) |
| The plugins themselves (what each does, what it offers other plugins) | `docs/plugins/` in DevHub-Plugins |

## Namespaces at a glance

| Namespace | Package | Main types |
|---|---|---|
| `DevHub.Sdk.Core.Settings` | Core | `ISettingsStore`, `ISettingsSection`, `AppSettings`, `SettingsStoreExtensions` |
| `DevHub.Sdk.Core.Plugins` | Core | `PluginInfo`, `PluginResolver`, `PluginSelection` |
| `DevHub.Sdk.Core.Plugins.Marketplace` | Core | `PluginApi`, `PluginPackageInfo`, `Marketplace`, `PluginInstaller`, `PluginFolders`, `InstallPlanner` |
| `DevHub.Sdk.Core.Localization` | Core | `L` |
| `DevHub.Sdk.Core.Search` | Core | `FuzzyMatcher`, `FuzzyMatch` |
| `DevHub.Sdk.Core.Formatting` | Core | `Markdown`, `PathTree`, `RelativeTime` |
| `DevHub.Sdk.Core.Charts` | Core | `ChartData`, `ChartSeries`, `ChartKind`, `ChartKinds`, `ChartMath` |
| `DevHub.Sdk.Core.Tables` | Core | `TableData` |
| `DevHub.Sdk.Core.People` | Core | `Person`, `PeopleSearch` |
| `DevHub.Sdk.Core.Collections` | Core | `ListMoves` |
| `DevHub.Plugins` | Sdk | `IPlugin`, `PluginContext`, `DevHubPluginAttribute` |
| `DevHub.Actions` | Sdk | `AppAction`, `IActionProvider`, `ActionRegistry`, `ActionLayout`, `MenuBuilder`, `Gestures`, XAML extensions |
| `DevHub.ViewModels` (and below) | Sdk | `ViewModelBase`, `DocumentViewModelBase`, `IDocumentFactory`, `IDocumentService`, `ToolWindowViewModelBase`, `DialogViewModelBase`, `ConfirmDialogViewModel`, `SettingsPageViewModelBase`, widget bases, `AvatarViewModel` |
| `DevHub.Services` | Sdk | `IDialogService`, `IStatusService`, `IThemeService`, `IExportService` |
| `DevHub.Integration` | Sdk | hooks between plugins: projects, Git, GitHub, diagrams, issues, charts |
| `DevHub.Controls` | Sdk | `Icon`, `IconKind`, `ActionButton`, `Toolbar`, `ToolWindowFrame`, `EmptyState`, `SettingsRow`, `SidePane`, charts, drag and drop, … |
| `DevHub.Converters` | Sdk | `FractionConverter`, `HexToBrushConverter`, `IndentConverter` |
| `DevHub.Localization` | Sdk | `{l:T}` (`TExtension`) |

## A minimal plugin

```csharp
using DevHub.Actions;
using DevHub.Plugins;
using DevHub.Sdk.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;

[assembly: DevHubPlugin(typeof(Hello.HelloPlugin))]

namespace Hello;

public sealed class HelloPlugin : IPlugin
{
    public const string Id = "naurid.hello";

    public PluginInfo Info { get; } = new(Id, "Hello", "Says hello from Find Action.", []) { Category = "Tools" };

    public void ConfigureServices(IServiceCollection services, PluginContext context) =>
        services.AddSingleton<IActionProvider, HelloActions>();
}

internal sealed class HelloActions(DevHub.Services.IStatusService status) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction("Hello.SayHello", "Say Hello",
            new CommunityToolkit.Mvvm.Input.RelayCommand(() => status.Show("Hello!")))
        {
            Category = "Hello",
            Menu = "Tools",
        };
    }
}
```

Next to it a `plugin.json` with the same id, `"apiVersion": 1` (`PluginApi.Version`) and no version (it's computed
from git in DevHub-Plugins). [PLUGINS.md §7](../PLUGINS.md) is the full recipe, DevHub-Plugins' `README.md` the
build, F5 and publishing steps.

## Compatibility

- **`PluginApi.Version`** (`DevHub.Sdk.Core.Plugins.Marketplace`) is the plugin API version. A plugin's
  `plugin.json` must name it as `apiVersion`; the build fails when it doesn't match the Sdk package
  (`buildTransitive/DevHub.Sdk.Core.targets`), and DevHub refuses plugins made for another one.
- A change that removes or changes a public member plugins use is **breaking**: bump `PluginApi.Version` and say
  so in the release notes. Adding members isn't breaking.
- The Sdk's assembly version is the plugin API version (`N.0.0.0`), not DevHub's, so a plugin built against a
  newer Sdk still loads in an older DevHub of the same API version.
- Plugins must use the same Avalonia, CommunityToolkit.Mvvm and Microsoft.Extensions.DependencyInjection versions
  as DevHub: DevHub provides those assemblies at run time.
