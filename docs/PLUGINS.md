# DevHub plugin architecture: design + migration plan

## Context
The user wants DevHub to be built **entirely from plugins**, so each dev gets an app shaped to
their needs. A dev who only wants a Git client should get a shell plus Git, with no template
menus, pages or shortcuts. Decisions confirmed on 2026-10-11:
- **Authors:** only first-party plugins for now, but with a real contract shaped so other devs
  can write plugins later.
- **Distribution:** one install that ships every plugin. Plugins are switched on and off in
  Settings → Plugins, plus a first-run picker. A disabled plugin is **not loaded at all**.
- **Switching** needs a restart ("Restart to apply"). DI is built once at startup with the
  enabled plugins.
- **Dependencies:** hard dependencies are declared (GitHub requires Git). Soft integrations go
  through optional hook interfaces (e.g. "Open in Git" in Projects appears only when Git is on).
- **Base shell:** bare. Window, title bar, status bar, editor tabs, tool-window frame,
  Settings (Appearance + Plugins), Find Action and Welcome. Even Projects is a plugin.
- **Code layout:** two projects per plugin: `X.Core` (logic, no Avalonia, unit-tested) and
  `X` (views, ViewModels, actions).
- **Timing:** now, before M1. Templates will be written as a plugin from the start.
- **First run:** presets ("Git client", "Project templates", "Everything") above a checklist.

Status: **P1 (Core split + plugin settings sections), P2 (`DevHub.Sdk`) and P3 (plugin contract +
extension points) done.**
P3 as built: `PluginInfo` / `PluginResolver` in Sdk.Core (tested); `IPlugin`, `PluginContext`,
`[DevHubPlugin]`, `TitleBarWidgetViewModelBase` / `StatusBarWidgetViewModelBase` in the Sdk;
`PluginHost` in the app scans `DevHub*.dll`, resolves against `AppSettings.EnabledPlugins` (null =
all) before DI is built, and shows a status note for plugins that couldn't load. Placement is
action metadata (`Menu`/`MenuOrder`, `ToolbarOrder`, `WelcomeOrder`; documents via
`Navigation`, tool windows via `Order`). `{a:Action id}` hides its control when the id's plugin is
off. Git owns `ActiveRepositoryService` (the shell only has `DocumentViewModelBase.ContextPath`).
Debug builds validate all DI registrations at startup. Left for P4: the plugins' own assemblies,
their own token files, `IRemoteHostProvider` (GitHub is already optional for Git: null-tolerant).
P2 notes: namespaces didn't change (`DevHub.Controls` etc. now span Sdk + App); XAML uses
`using:` namespaces; the shell sets the base classes' host-only members through
`InternalsVisibleTo("DevHub.App")`; markup extensions get the container from `AppServices.Current`. Progress is tracked in `docs/ROADMAP.md` (milestone P).
Once P6 lands, this file becomes the reference for the contract plus a recipe for writing a plugin.

## Target solution layout
```
src/
  DevHub.Sdk.Core/        no Avalonia: plugin manifest model + PluginResolver, settings store
                          (typed sections), FuzzyMatcher, Formatting (PathTree, RelativeTime)
  DevHub.Sdk/             Avalonia: what every plugin builds against: IPlugin, ViewModelBase,
                          DocumentViewModelBase/IDocumentFactory, ToolWindowViewModelBase,
                          DialogViewModelBase, SettingsPageViewModelBase, AppAction/IActionProvider,
                          IDialogService/IStatusService/IThemeService, Controls, Themes + tokens,
                          shared icons, contribution interfaces (below)
  DevHub.App/             the shell exe: MainWindow, title/status bar, DocumentManager,
                          ToolWindowManager, ActionRegistry/Keymap/MainMenu, Find Action, Settings
                          dialog, Appearance page, Plugins page, first-run, PluginHost (loader)
plugins/
  DevHub.Projects.Core/   Projects + Repositories (merged: they form a cycle), LanguageToolChoice
  DevHub.Projects/        Projects tool window, Open With page, ExternalEditor, TerminalLauncher
  DevHub.Git.Core/        today's Core/Git
  DevHub.Git/             repository/diff/blame tabs, Git Console, Git actions, branch widget
  DevHub.GitHub.Core/     today's Core/GitHub
  DevHub.GitHub/          GitHub settings page, GitHubSession, Avatar, Publish/Clone-from-GitHub hooks
  (later) DevHub.Templates.Core / DevHub.Templates   ← M1–M9 land here
tests/
  DevHub.TestKit/         TempGitRepo, [GitFact] (shared by Git + GitHub tests)
  DevHub.Sdk.Core.Tests, DevHub.Projects.Core.Tests, DevHub.Git.Core.Tests, DevHub.GitHub.Core.Tests
Directory.Build.props     net10.0, Nullable, ImplicitUsings in one place
```
Dependency rule: plugins → Sdk (→ Sdk.Core) and → their own `.Core`, and → another plugin's
`.Core`/UI only when it's a **declared hard dependency**. The shell (App) references
Sdk, plus every plugin only so they get copied into the output. It never uses a plugin's types.

## The plugin contract (DevHub.Sdk)
```csharp
[assembly: DevHubPlugin(typeof(GitPlugin))]          // how the host finds it in an assembly

public sealed class GitPlugin : IPlugin
{
    public PluginInfo Info { get; } = new(
        Id: "devhub.git", Name: "Git", Description: "Fork-style Git client…",
        Version: "1.0", Requires: ["devhub.projects"], Category: "Version control");

    public void ConfigureServices(IServiceCollection services, PluginContext context) { … }
    public IEnumerable<Uri> Resources => [new("avares://DevHub.Git/Themes/GitTokens.axaml")];
}
```
- **Discovery:** the `PluginHost` scans `DevHub.*.dll` next to the exe for
  `[DevHubPlugin]`. Later the same scan covers a user plugins folder, which is how
  third-party plugins arrive. The shell never needs a hardcoded list.
- **Startup:** load settings, then `PluginResolver.Resolve(all, enabled)`. The resolver
  adds required plugins, drops plugins whose dependencies are missing (with a reason), and
  rejects cycles. Each resolved plugin's `ConfigureServices` runs and its `Resources` are
  merged into the app. Then DI is built. `PluginContext` holds the plugin's settings section
  and the app data directory.
- **A plugin contributes only through DI** into these extension points:

| Extension point | Today | Change |
|---|---|---|
| Actions | `IActionProvider` (open) | `AppAction` gains `Menu` ("Git", "File/New", order) and `Placement` flags (Toolbar, Welcome). `MainMenu` builds groups from that, not hardcoded ids. Plugin action ids are prefixed by plugin (`Git.*`). |
| Documents | `DocumentViewModelBase` / `IDocumentFactory` (open) | unchanged. Tabs from a disabled plugin are kept in the layout but skipped. |
| Tool windows | `ToolWindowViewModelBase` (open) | add `Order`. Alt+N follows `Order`, not DI order. |
| Settings pages | hardcoded ctor in `SettingsDialogViewModel` | `IEnumerable<SettingsPageViewModelBase>` + `Order`. |
| Title bar | branch widget hardcoded in `MainToolbar.axaml` | `ITitleBarWidget` (ViewModel + `Order`), shown through ViewLocator. Git contributes the branch widget. |
| Status bar | theme/density hardcoded | `IStatusBarWidget`, same pattern (theme/density become shell widgets). |
| Welcome | hardcoded action list | actions with `Placement.Welcome`. |
| Theme tokens | feature tokens in shell (`Brush.Diff.*`, `Graph.*`, branch widget sizes) | moved into the plugin's own `Resources` dictionary. Shared tokens stay in Sdk. |
| Icons | `Icon` hardcodes `avares://DevHub.App/Assets/Icons/` | shared set moves to Sdk. `Icon.Kind` also accepts a full `avares://` URI for icons a plugin ships. |
| Views | `ViewLocator` uses `Type.GetType(name)` (calling assembly only) | use `vm.GetType().Assembly.GetType(name)`, so views resolve in plugin assemblies. |
| Settings | one typed `AppSettings` listing every feature | the shell keeps typed shell sections. Plugins get `store.Section<T>(pluginId)` / `UpdateSection<T>`, stored under `"plugins": { "devhub.git": {…} }` as JSON. `[JsonExtensionData]` keeps unknown data (e.g. of disabled plugins). A one-time migration moves `git`, `languageTools` and `repositoryFolders` into their plugin sections (tested). |

**Removing the shell → Git links:** `DocumentManager.ActiveRepository` and
`DocumentViewModelBase.RepositoryPath` become generic. The base exposes `virtual string?
ContextPath` (the folder a tab is about). The Git plugin owns an `IActiveRepository` service
that watches `DocumentManager.Active`, so Git actions and the branch widget use that.

**Soft hooks (optional integrations):**
- `IProjectOpener` (defined by Projects, implemented by Git): "Open in Git" or double-click
  in Projects opens a repository tab. Without Git, Projects uses its own Open With.
- `IRemoteHostProvider` (defined by Git, implemented by GitHub): Publish to GitHub,
  Clone-from-account list, commit avatars. Without GitHub, Git still does remotes by URL.
- Today's `RepositoryTabServices` then holds only Git's own things, plus Projects' editor and
  terminal launcher (Git requires Projects).

**Dependency graph:** Projects ← Git ← GitHub, and Templates → Projects. Templates can run
`git init` as a post-create command, so it doesn't need Git. A Git-only dev therefore gets
the shell + Projects + Git. Projects is how you list and open repositories. If later we want
Git without the Projects list, Git can provide an "Open Repository…" action itself and the
dependency becomes a soft hook. That's a small change.

## Migration in phases (each ends building, with tests green and the app working the same)
1. **Core split (pure moves):** add `Directory.Build.props`. Create Sdk.Core,
   Projects.Core (merging Repositories and taking `LanguageToolChoice`, which removes both
   cycles), Git.Core and GitHub.Core. Split the tests and move `TempGitRepo` into TestKit.
   Settings sections + migration, tested.
2. **Sdk project:** move the base classes, Controls, Themes, Actions contracts and Services
   interfaces out of App. Fix ViewLocator, `Icon`, and the `avares://DevHub.App/...` URIs
   that move.
3. **Open the extension points** while the features are still inside App:
   - `IPlugin` + `PluginHost` + `PluginResolver` (tested).
   - Menus from action metadata, settings-page list, title-bar and status-bar widgets,
     Welcome placement, tool-window `Order`.
   - Remove `ActiveRepository`.
   - Wrap each feature in an `IPlugin` class registered by the host.
   - `App.ConfigureServices` keeps only shell services.
4. **Move features out**, one assembly pair at a time: Projects, then Git, then GitHub.
   The Git→GitHub links become `IRemoteHostProvider`, and Projects→Git becomes
   `IProjectOpener`. The placeholder Templates/Generate/Import pages are deleted (M1
   rebuilds them as the Templates plugin). UI Gallery stays a DEBUG-only shell page.
5. **Plugins UI:** a Settings → Plugins page (list, description, requires, on/off, "Restart
   to apply", turning one off warns about plugins that depend on it), a first-run page
   with presets + checklist, and a status note when a plugin was skipped (missing dependency).
6. **Docs:**
   - New `docs/PLUGINS.md`: the contract, extension points, and a recipe for writing a
     plugin, with Git as the worked example.
   - Update the CLAUDE.md layout and rules, `UI_GUIDE.md` §5, and the `avalonia-mvvm` and
     `template-engine` skills (new paths).
   - `ROADMAP.md`: a new "P — Plugin architecture" milestone with P1–P6. M1+ then target
     `DevHub.Templates(.Core)`, and M10's tool catalog becomes a plugin contribution
     (`ICommandTool`).

## Verification
- After every phase: `dotnet build` (0 warnings) and `dotnet test` green, and the app is
  launched with `DEVHUB_DATA_DIR` set to a scratch folder.
- New tests:
  - `PluginResolver`: dependency closure, missing dependency, cycle, disabling a dependency.
  - Settings migration: an old `settings.json` becomes plugin sections; unknown sections
    survive a save.
  - Discovery: finds `[DevHubPlugin]` in test assemblies.
- Manual, per preset:
  - **"Git client":** no Templates menu or Welcome links, and no template actions in Find
    Action. Projects + Git work as today.
  - **Git off:** no Git menu, branch widget, Git Console or Alt+N slot, and repository
    tabs are kept but not restored.
  - **GitHub off:** Git works, and Publish to GitHub and avatars are gone.
  - **Turn everything on again:** the old tabs and settings come back.
