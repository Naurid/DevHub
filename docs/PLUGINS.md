# DevHub plugins

DevHub is a **bare shell plus plugins**. Every feature (Projects, Git, GitHub, Templates, …) is
a plugin the user switches on or off, so each developer gets an app with only what they use: a
Git-only setup has no template menus, tabs, tool windows or shortcuts at all.

This file is the reference: how plugins are found and loaded (§2), the contract (§3), where a
plugin can contribute (§4), its settings (§5), how plugins link up (§6), a step-by-step recipe
for writing one (§7) and how to test it (§8). Background and history are in §9.

Related: `docs/UI_GUIDE.md` (rules and recipes for views, actions, tabs, tool windows, settings
pages), `docs/ACTIONS_AND_FUZZY_MATCHING.md` (actions, menus, shortcuts), `CLAUDE.md` (rules).

---

## 1. Layout and what may reference what

```
src/
  DevHub.Sdk.Core/      no UI: PluginInfo, PluginResolver, PluginSelection, settings store
  DevHub.Sdk/           what every plugin UI builds on: IPlugin, base classes, extension points,
                        Integration/ (hooks between independent plugins), design system
  DevHub.App/           the shell: window, tabs, tool-window frame, menus, Settings, Find Action,
                        PluginHost, Plugins page, first-run picker
plugins/
  DevHub.<Name>/        a plugin's UI: <Name>Plugin.cs, Actions/, ViewModels/, Views/, Themes/
  DevHub.<Name>.Core/   its logic: no Avalonia, unit-tested (optional for a UI-only plugin)
tests/
  DevHub.<Name>.Core.Tests/
```

| Project | May reference |
|---|---|
| `DevHub.<Name>.Core` | `DevHub.Sdk.Core` (+ a required plugin's `.Core`) |
| `DevHub.<Name>` | `DevHub.Sdk`, its own `.Core` (+ a required plugin's projects) |
| `DevHub.App` | `DevHub.Sdk`; the plugins only with `Aliases="plugins"` (shipped, never used) |
| `DevHub.Sdk`, `DevHub.Sdk.Core` | no plugin, never the app |

A plugin references another plugin **only if it declares it in `Requires`** (GitHub → Git).
Everything else links through hooks (§6). Projects and Git never reference each other.

## 2. Lifecycle

1. **Discovery.** At startup `PluginHost` (shell) loads every `DevHub*.dll` next to the exe and
   reads `[assembly: DevHubPlugin(typeof(…))]`. Plugins are sorted by `PluginInfo.Order`, then id.
   No list of plugins exists anywhere in the shell. (A user plugins folder can join the same scan
   later, for third-party plugins.)
2. **Resolution.** `PluginResolver.Resolve(all, AppSettings.EnabledPlugins)`: the switched-on
   plugins plus their requirements, requirements first. A plugin whose requirement is missing,
   a circle of requirements and a duplicate id are **skipped with a reason** (status bar at
   startup, Settings → Plugins). `EnabledPlugins` unset = first start: everything loads and
   the first-run picker opens.
3. **Registration.** Each loaded plugin's `ConfigureServices(services, context)` runs, in
   dependency order, **before the DI container is built**. A switched-off plugin is never called,
   so none of its services, menu items, tabs, tool windows or shortcuts exist.
4. **Resources.** Each loaded plugin's `Resources` (resource dictionaries, e.g. its design
   tokens) are merged into the app's resources before any window opens.
5. **Validation (Debug).** The container is built with `ValidateOnBuild`: a registration that
   can't be built (a missing dependency) fails at startup, not on some click. Debug builds also
   print `DevHub plugins: found …; loaded …` to stdout.
6. **Changes need a restart.** Settings → Plugins saves `EnabledPlugins`; Restart Now (or
   File → Restart DevHub) starts a new process and closes this one normally (tabs and tool
   windows are saved and come back). Tabs of a plugin that's off are kept in the saved layout,
   skipped while it's off, and reopen when it's back.

## 3. The contract

```csharp
// plugins/DevHub.Git/GitPlugin.cs
[assembly: DevHub.Plugins.DevHubPlugin(typeof(DevHub.Plugins.GitPlugin))]

namespace DevHub.Plugins;

public sealed class GitPlugin : IPlugin
{
    public const string Id = "devhub.git";

    public PluginInfo Info { get; } = new(Id, "Git",
        "A Fork-style Git client: changes, staging per line, commits, history graph, …",
        Requires: [])                         // GitHub: [GitPlugin.Id]
    {
        Category = "Version control",         // grouping on the Plugins page
        Order = 200,                          // lists, menus, load order among unrelated plugins
        Presets = ["Git client"],             // first-run presets it belongs to
    };

    // Optional: dictionaries merged into the app's resources (its own design tokens).
    public IEnumerable<Uri> Resources => [new("avares://DevHub.Git/Themes/GitTokens.axaml")];

    public void ConfigureServices(IServiceCollection services, PluginContext context)
    {
        services.AddSingleton<IActionProvider, GitActionProvider>();
        // … every service and contribution of the plugin (§4)
    }
}
```

| Member | Meaning |
|---|---|
| `PluginInfo.Id` | Stable, lowercase, dotted (`devhub.git`). Also its settings section key. Never rename. |
| `Name`, `Description`, `Category` | Shown on the Plugins page and in the first-run picker. |
| `Requires` | Ids it can't work without; they're switched on with it and load first. |
| `Order` | Position in lists and the main menu (Projects 100, Git 200, GitHub 300, Templates 400). |
| `Presets` | First-run presets it belongs to; the picker offers every preset named, plus Everything. |
| `PluginContext` | `Settings` (the store) and `LoadedPlugins` (ids loading this run). |

The plugin class has a public parameterless constructor and does nothing but describe and
register: no work, no I/O.

## 4. Extension points

A plugin contributes **only by registering things in DI**; the shell picks up every
registration of these types.

| To add … | Register | Notes / example |
|---|---|---|
| Actions (menu items, toolbar buttons, Welcome links, shortcuts, Find Action) | `IActionProvider` | Placement is metadata on `AppAction`: `Menu` + `MenuOrder`, `ToolbarOrder`, `WelcomeOrder` (a new hundred = a new group). Ids in the plugin's own ids class. `GitActionProvider` |
| An editor tab (one instance) | `DocumentViewModelBase` (singleton) | Its "open" action: override `Navigation` (label, shortcut, menu, Welcome). `TemplatesViewModel` |
| Tabs per item (per repository, per file) | `IDocumentFactory` | Ids `Prefix:` + key; restored at startup. `RepositoryDocumentFactory` |
| A tool window | the VM as itself + as `ToolWindowViewModelBase` | `Order` (stripes, Alt+N), `WelcomeOrder`. `RepositoriesViewModel` |
| A Settings page | `SettingsPageViewModelBase` (**transient**) | `Order` in the list. `OpenWithSettingsPageViewModel` |
| A title-bar widget | `TitleBarWidgetViewModelBase` | `BranchWidgetViewModel` |
| A status-bar widget | `StatusBarWidgetViewModelBase` | left of theme / density |
| Design tokens | `IPlugin.Resources` | `Themes/GitTokens.axaml` (Dark + Light `ThemeDictionaries`; no Compact variant yet) |
| Icons | `Icon Kind="avares://DevHub.Foo/Assets/Icons/x"` | shared icons are in the Sdk (`IconKind`) |
| A dialog | nothing (open it with `IDialogService`) | its View next to its VM in the plugin |
| Services for itself / plugins that require it | anything | `GitPlugin.Executable`, `ActiveRepositoryService` |

What the shell offers plugins (inject them): `IDialogService`, `IStatusService`, `IThemeService`,
`ISettingsStore`, `IDocumentService` (active tab, open tabs, open by id). Views and ViewModels
follow `docs/UI_GUIDE.md`; a View is found next to its ViewModel (same assembly, name
`…ViewModel` → `…View`), and XAML namespaces are `using:`. An `{a:Action id}` whose plugin is off
hides its control, so a view may offer another plugin's action by id.

## 5. Settings

A plugin's settings are a class implementing `ISettingsSection`, stored in settings.json under
`"plugins": { "<SectionId>": { … } }`:

```csharp
public sealed class GitSettings : ISettingsSection   // in DevHub.Git.Core
{
    public static string SectionId => "devhub.git";  // = the plugin id
    public bool ChangesAsTree { get; set; } = true;
}

var git = store.LoadSection<GitSettings>();                       // defaults when never saved
store.UpdateSection<GitSettings>(s => s.ChangesAsTree = value);   // load → change → save
```

Sections of switched-off (or unknown) plugins are kept untouched when others save. Never add
plugin fields to `AppSettings` (that's the shell's: window layout, appearance, `EnabledPlugins`).

## 6. Linking plugins

**Hard requirement** (`Requires`): the plugin may use the required plugin's types and services
directly. GitHub requires Git.

**Optional link between independent plugins:** a small interface in `src/DevHub.Sdk/Integration/`,
with only plain data (paths) in it. The plugin that offers the feature registers an
implementation; the one that uses it takes `IEnumerable<T>` (first or none) or `T? x = null`,
and **always has a fallback**:

| Interface | Offered by | Used by | Without it |
|---|---|---|---|
| `IProjectOpener` | Git (repository tab) | Projects: double-click / Enter | opens the project in its tool |
| `IProjectFileEditor` | Projects (Open With) | Git: Open in Editor (F4), conflicts | the Windows default app |
| `IProjectFolders` | Projects (scan folders, rescan) | Git: Clone's folder, rescan after | Documents, no rescan |
| `IAddFilesFromTemplate` | Templates (opens Add Files on a folder) | Projects (right-click a project), Git (repository tab button) | the entries don't show |

**A hook owned by the required plugin:** even with a hard requirement, keep the required plugin
free of the one that needs it. Git defines `IRemoteHost` (in `DevHub.Git`) and GitHub implements
it (`GitHubRemoteHost`): Publish, your avatar on your commits, your repositories in Clone. Git
takes `IRemoteHost? host = null`; without GitHub, Publish isn't offered and Clone takes a URL.

**Between plugins and the shell:** the shell never names a plugin. If the shell would need to
know a feature, add an extension point instead (§4 grew that way: widgets, placement metadata,
`ContextPath` on tabs, `IDocumentService`).

## 7. Recipe: a new plugin

Example: a `Docker` plugin with a tool window listing containers, an action and a setting.
Swap in your feature's name. Look at `plugins/DevHub.Projects` for a small real one.

1. **Projects.** Create `plugins/DevHub.Docker.Core/DevHub.Docker.Core.csproj` (logic) and
   `plugins/DevHub.Docker/DevHub.Docker.csproj` (UI), and a test project:
   ```xml
   <!-- DevHub.Docker.csproj (Directory.Build.props supplies net10.0, Nullable, ImplicitUsings) -->
   <Project Sdk="Microsoft.NET.Sdk">
       <PropertyGroup>
           <RootNamespace>DevHub</RootNamespace>
           <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
       </PropertyGroup>
       <ItemGroup><PackageReference Include="Avalonia" Version="12.1.3" /></ItemGroup>
       <ItemGroup>
           <ProjectReference Include="..\..\src\DevHub.Sdk\DevHub.Sdk.csproj" />
           <ProjectReference Include="..\DevHub.Docker.Core\DevHub.Docker.Core.csproj" />
       </ItemGroup>
   </Project>
   ```
   The `.Core` project references `src/DevHub.Sdk.Core` only. Add all three to `DevHub.sln`
   (`dotnet sln add … --solution-folder plugins` / `tests`), and the UI project to
   `src/DevHub.App/DevHub.App.csproj` with `Aliases="plugins"` so it ships.
2. **Logic first, tested.** Put everything that doesn't need a window in `DevHub.Docker.Core`
   (running `docker`, parsing its output) with tests in `tests/DevHub.Docker.Core.Tests`. Run
   external tools with `ProcessStartInfo` + `ArgumentList`, never a shell string.
3. **The plugin class.** `plugins/DevHub.Docker/DockerPlugin.cs` with the attribute, `Info`
   (id `devhub.docker`, `Category`, `Order`, `Presets`, `Requires` only if truly needed) and
   `ConfigureServices`.
4. **Settings (if any).** `DockerSettings : ISettingsSection` (`SectionId => "devhub.docker"`) in
   the `.Core` project; read with `LoadSection`, change with `UpdateSection`, round-trip test.
5. **UI.** Follow `docs/UI_GUIDE.md` §5, with the plugin's folders:
   - tool window: `ViewModels/ToolWindows/ContainersViewModel.cs` + `Views/ToolWindows/ContainersView.axaml`;
     register it as itself and as `ToolWindowViewModelBase`, set `Order`;
   - actions: `Actions/DockerActionIds.cs` + `Actions/Providers/DockerActionProvider.cs`, with
     `Menu = "Docker"` (its own main-menu group) and an order per action;
   - a Settings page if it has settings (transient `SettingsPageViewModelBase`).
   Shared tokens and components only (no hex / sizes in views); a token only Docker uses goes in
   `Themes/DockerTokens.axaml`, returned from `Resources`.
6. **Links.** Need another plugin's feature? Declare `Requires`, or (if both should stand alone)
   use / add an `Integration` hook with a fallback (§6).
7. **Docs.** Add the plugin to the README's plugin list and, if it adds a hook, to §6 here.

## 8. Testing a plugin

- `dotnet build` (0 warnings) and `dotnet test`.
- **Plugin combinations:** `pwsh tools/plugin-matrix.ps1` (Debug build) starts DevHub with
  everything, with nothing, and with each plugin alone (add `-Extra "devhub.a,devhub.b"` for
  more), each in its own scratch data folder, and reports whether it stayed up and what loaded.
  A plugin that only works next to another one, or a shell that still names a plugin, fails here.
- **By hand**, with `DEVHUB_DATA_DIR` set to a scratch folder: your plugin alone (its menu, tool
  window, actions, Find Action entries, Settings page), then switched off in Settings → Plugins
  (after Restart Now nothing of it is left), then on again (its tabs and settings come back).

## 9. Marketplace plugins (outside this repository)

Plugins can also live outside DevHub: in **Naurid/DevHub-Plugins** (its starter is
`tools/plugin-repo/` here, with a sample HelloWorld plugin), installed from Settings → Plugins →
Marketplace.

- **Developing one:** `DEVHUB_PLUGIN_PATHS` (`;`-separated) lists folders DevHub loads plugins from:
  a plugin folder (plugin.json next to the DLLs, e.g. a plugin's build output) or a folder of them. A
  plugin there wins over any bundled or installed copy and is switched on for that run (settings
  unchanged); a path that doesn't exist or holds no plugin is reported. The plugins repo's build writes
  a `launchSettings.json` that starts the installed DevHub this way (F5 debugs a plugin inside DevHub),
  and its `build/Test-Plugins.ps1` runs the plugin matrix against those folders. With the variable set,
  even a Release DevHub prints its `DevHub plugins: …` line (with problems) to stdout.
- **Sdk:** a marketplace plugin builds against the NuGet packages `DevHub.Sdk` + `DevHub.Sdk.Core`
  (nuget.org, DevHub's version; `build/SdkPackage.props`), referenced build-only
  (`ExcludeAssets="runtime"`). For a project with a `plugin.json`, the package's build check fails
  when `apiVersion` differs from the Sdk's `PluginApi.Version`. Unreleased Sdk changes: `pwsh
  build/Pack-Sdk.ps1` → `%LocalAppData%\DevHub-local-feed` (docs/RELEASING.md).
- **Package:** a zip with `plugin.json` (id, name, version, description, category, `apiVersion`,
  requires) and the plugin's own DLLs, never the Sdk or Avalonia (DevHub has those).
- **Index:** `index.json`, an array of entries (id, name, version, apiVersion, url, sha256, size…).
  DevHub reads the sources in `AppSettings.PluginSources`, by default `PluginApi.DefaultIndexUrl`
  (the "index" release of DevHub-Plugins). A source may also be a local index.json, whose URLs may
  be `file:///` (to try your own plugin).
- **Install, update, remove:** `PluginInstaller` (Sdk.Core). Downloads are checked against the
  SHA-256, extraction refuses entries outside the plugin folder, and changes wait in
  `%AppData%\DevHub\plugins\.pending` until the next start. Then `PluginHost` moves them into place,
  loads `plugins\<id>\*.dll`, and switches the plugin on.
- **Requirements:** installing a plugin installs what its `requires` lists and isn't there yet (built in,
  installed or waiting for the restart), requirements of requirements included, each before the plugin
  that needs it; the confirmation lists them all. A requirement no source offers, one made for another
  plugin API, or a circle stops the install before anything is downloaded, and a failure half-way drops
  what this install had unpacked. Removing a plugin that installed plugins need asks first and removes
  them with it. The logic is `InstallPlanner` (Sdk.Core, tested); load order still comes from
  `PluginResolver`.
- **One copy of each DLL:** a package leaves out the DLLs of the plugins it requires (the plugins
  repo's `Pack-Plugin.ps1`), and `PluginHost` loads marketplace folders requirements first
  (`PluginResolver.RequirementsFirst`), skipping a DLL whose name is already loaded: a required
  plugin's own DLLs always win over a copy another package brings.
- **Sdk assembly version = plugin API version** (`N.0.0.0`, `build/SdkPackage.props`); the package and
  file versions are DevHub's. A plugin built against a newer Sdk with the same plugin API loads in an
  older DevHub (and in a `0.0.0` development build), as long as it only uses members that DevHub has.
- **Compatibility:** `PluginApi.Version` (Sdk.Core) is the plugin API version. A plugin whose
  `apiVersion` differs isn't installed or loaded. **Bump it when a change to the Sdk breaks plugins**
  (a removed or changed member they use), and say so in the release notes.
- **Bundled plugins and their updates:** a plugin folder in `plugins\` next to DevHub's executable is
  *bundled* (installed and updated with DevHub: Projects, Git, GitHub and Templates, fetched from the index by
  `build/Get-BundledPlugins.ps1` in `release.yml`; a Debug build fetches them once into its output).
  When the marketplace folder has the same plugin, the newer version loads and only that folder's DLLs
  are touched (`PluginFolders.Choose`, Sdk.Core, tested): a marketplace update replaces the bundled
  copy until a DevHub update bundles a version at least as new. An update made for another plugin API
  is ignored, so it never hides a working bundled copy. The Marketplace page shows "Bundled 1.2.0",
  **Update to 1.3.0**, then **Back to 1.2.0** (removes the update) or **Remove Old Copy** (an
  installed copy older than the bundled one).
- **Built-ins win:** a plugin folder with the id of a built-in plugin (its DLL next to the app, as
  in development builds) isn't loaded.
- **Versions:** each marketplace plugin has its own (major tags `<id>/vN`, +1 minor per change to
  its folder on main); built-in plugins share DevHub's version.

## 10. Background

Decided on 2026-10-11. Plugins are first-party for now, with a contract shaped for third parties
later. One install ships every plugin, and users switch them on and off with a first-run picker
and Settings → Plugins, applied after a restart. Hard dependencies are declared; optional links
go through hooks. The shell is bare (even Projects is a plugin). Each plugin has a UI project
plus a `.Core` project. Later the same day: Projects and Git must not depend on each other in
either direction.

Built in phases P1–P6 (`docs/ROADMAP.md`, milestone P), each ending with a green build:
- **P1:** the old `DevHub.Core` split into `Sdk.Core` + one `.Core` per plugin, with settings sections and their migration.
- **P2:** `DevHub.Sdk`.
- **P3:** the plugin contract and the extension points.
- **P4:** the plugins moved into their own assemblies.
- **P5:** the Plugins page and the first-run picker.
- **P6:** these docs and `tools/plugin-matrix.ps1`.

Settings files from before P1 are migrated on load.

Known limits: plugin token dictionaries have no Compact variant (keep shrinking tokens in the
Sdk); plugins are loaded from the app folder only; switching needs a restart.
