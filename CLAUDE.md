# DevHub — Project Guide

Desktop app for setting up **templates for new projects** (any language/framework:
a folder hierarchy plus starter files) and **file/script templates** (e.g. a C# class,
a Unity MonoBehaviour, a Python module) stamped into existing folders. You pick a
template, fill in variables, and DevHub generates the files and optionally runs
post-create commands (`git init`, `dotnet restore`, `npm install`, ...).

Completely standalone — unrelated to any other project on this machine.

## Stack
- .NET 10 target (`net10.0`) — the current LTS (installed SDK: 10.0.401). Avalonia 12's
  source generators need Roslyn ≥ 4.14, which the .NET 10 SDK provides. Symptom on an
  older SDK: warning CS9057 ("analyzer references version 4.14.0.0 of the compiler")
  followed by `CS0103: The name 'InitializeComponent' does not exist`. Fix = update the
  SDK, not the code.
- Avalonia **12.x** (12.1.3 was latest stable on 2026-09-25) + Fluent theme + Inter font
- CommunityToolkit.Mvvm 8.4.x (`[ObservableProperty]`, `[RelayCommand]` source generators)
- Microsoft.Extensions.DependencyInjection for composition
- System.Text.Json for manifests and settings
- xUnit for the `*.Core.Tests` projects

**Avalonia 12 caution:** most Avalonia examples online (and in model training data)
target 11.x, and 12 has breaking changes. When you're unsure an API still exists
or a build error looks like an API change, check docs.avaloniaui.net or the 12.x
migration notes before guessing. Don't downgrade packages to make old snippets
compile.

## Solution layout
DevHub is a **bare shell + plugins** (milestone P, reference in
`docs/PLUGINS.md`, complete since P6); `README.md` shows the current state.

```
DevHub.sln   Directory.Build.props (net10.0, Nullable, ImplicitUsings for every project)
src/
  DevHub.Sdk.Core/        base for everything, NO Avalonia: settings store (shell sections +
                          one section per plugin, ISettingsSection), plugin model + resolver,
                          FuzzyMatcher, Formatting
  DevHub.Sdk/             Avalonia building blocks for the shell and every plugin UI: IPlugin, base
                          classes (ViewModelBase, DocumentViewModelBase, ToolWindowViewModelBase,
                          DialogViewModelBase, SettingsPageViewModelBase, widgets), IDocumentService,
                          action system, service interfaces, Integration/ (cross-plugin hooks),
                          Controls/ (incl. Avatar), Themes/ (shared tokens), Assets/Icons/
  DevHub.App/             the shell exe only: window, DocumentManager, ToolWindowManager, menus,
                          Settings, Find Action, PluginHost. References the plugins only to ship
                          them (alias "plugins": their types are off-limits here)
plugins/                  each plugin = X (UI: views, ViewModels, actions, its plugin class) + X.Core
  DevHub.Projects/ + .Core   Projects tool window, Open With (ProjectsSettings)
  DevHub.Git/ + .Core        Git client UI, IRemoteHost, Themes/GitTokens.axaml (GitSettings)
  DevHub.GitHub/ + .Core     GitHub account, Publish, GitHubRemoteHost (requires Git)
  DevHub.Templates/ + .Core  New Project (browser + generation flow); the engine (model, renderer, generation, BuiltInTemplates/)
tests/
  DevHub.TestKit/         TempGitRepo, [GitFact]/[GitTheory] (shared helpers, not a test project)
  DevHub.Sdk.Core.Tests/  DevHub.Projects.Core.Tests/  DevHub.Git.Core.Tests/  DevHub.GitHub.Core.Tests/
  DevHub.Templates.Core.Tests/
.github/                  CI (build, tests, security checks), CodeQL, Dependabot, release (Velopack → GitHub Releases)
build/Get-Version.ps1     the version from git (vN major tags, +1 minor per push on main)
build/Pack-Sdk.ps1        DevHub.Sdk + DevHub.Sdk.Core NuGet packages (settings: build/SdkPackage.props);
                          release.yml pushes them to nuget.org, without -Version → ../local-feed
tools/
  plugin-matrix.ps1       starts DevHub with each plugin combination
  plugin-repo/            starter for Naurid/DevHub-Plugins (marketplace plugins; its own CI + publish)
docs/
  RELEASING.md          versions, CI, releases, in-app updates, the one-time GitHub settings
  PLUGINS.md            plugins: lifecycle, contract, extension points, linking, recipe, marketplace
  TEMPLATE_FORMAT.md    the template spec — single source of truth
  ROADMAP.md            milestones + checkboxes; the "where are we" file
  UI_GUIDE.md           UI rules, tokens, components and recipes (read before App changes)
  ACTIONS_AND_FUZZY_MATCHING.md   how actions, menus, shortcuts and Find Action work
  GIT_CLIENT_PLAN.md    plan + rules for the integrated Git client (milestones G0–G9)
```

**Dependency direction:** `App → Sdk (→ Sdk.Core)` (+ the plugins, aliased, only to ship them);
a plugin's UI project → `Sdk` + its own `.Core`; a plugin's `.Core` → `Sdk.Core`; a plugin may
reference another plugin's projects only for a declared hard dependency (GitHub → Git); tests →
the project they test. **`Sdk.Core` never knows about a plugin, and nothing
references `App`; `Sdk` never references `App` or a plugin** (the shell may set the
base classes' host-only `internal` members via `InternalsVisibleTo`). All logic that can be tested without a window lives in a `.Core`
project (the "Core" in the rules below means "a `.Core` project"). If you catch yourself
writing file I/O, path math, or string templating in a ViewModel, move it to a Core
service and inject it.

**Features are plugins** (`IPlugin` in DevHub.Sdk; each plugin project has its plugin class,
e.g. `plugins/DevHub.Git/GitPlugin.cs`, marked `[assembly: DevHubPlugin(…)]`: Projects, Git,
GitHub → Git, Templates → Projects). A plugin's own design tokens go in its `Themes/*.axaml`,
returned from `IPlugin.Resources`; its views' `avares://` URIs name its own assembly.
Git → GitHub-style features go through Git's `IRemoteHost` (GitHub implements it). A plugin
names the first-run presets it belongs to in `PluginInfo.Presets`; users switch plugins in
Settings → Plugins (applied after a restart).
**Projects and Git must never depend on each other** (either direction); they link through the
`DevHub.Integration` interfaces in the Sdk (`IProjectOpener`, `IProjectFileEditor`,
`IProjectFolders`), and each has a fallback when the other is off. A plugin registers everything it adds in its `ConfigureServices`; the shell's
`App.ConfigureServices` holds only shell services. A switched-off plugin registers nothing, so:
- never hardcode a feature in the shell (menus, toolbar, Welcome come from action metadata:
  `Menu`/`MenuOrder`, `ToolbarOrder`, `WelcomeOrder`; title/status-bar widgets are contributions);
- a plugin uses another plugin's services only if it declares it in `Requires`; optional
  integrations go through a hook interface in `src/DevHub.Sdk/Integration/` (plain paths, no
  plugin types) that the other side may or may not register, injected as `IEnumerable<…>` or an
  optional `T? x = null` parameter, always with a fallback;
- Debug builds validate every DI registration at startup (`ValidateOnBuild`), so a missing
  dependency shows up immediately. Try plugin combinations with `enabledPlugins` in a scratch
  `DEVHUB_DATA_DIR` settings file; load a plugin straight from its build folder with
  `DEVHUB_PLUGIN_PATHS` (docs/PLUGINS.md §9).

**Plugin settings** live in their own section: a class implementing `ISettingsSection`
(`SectionId` = plugin id) in the plugin's `.Core`, read with `store.LoadSection<T>()`,
changed with `store.UpdateSection<T>(s => …)`. Don't add plugin fields to `AppSettings`.

## Build / run / verify
```
dotnet build
dotnet test
dotnet run --project src/DevHub.App
```
- Plugin or shell changes: also `pwsh tools/plugin-matrix.ps1` (Debug build): starts DevHub with
  every plugin alone, none and all, and reports what loaded / what crashed.
- Core changes: verify with `dotnet test` — add or extend tests for every engine
  behavior change (renderer, generator, importer, ignore rules). This is the one
  part of the app where regressions silently corrupt user files.
- UI changes: Claude can build but cannot see the window. Finish UI work with a
  short **manual check list** for the user (what to click, what should appear).

## Core rules (engine correctness — non-negotiable)
Full details in the `template-engine` skill and `docs/TEMPLATE_FORMAT.md`.
- **Unknown `{{...}}` tokens pass through untouched.** Templates contain Vue,
  Handlebars, Jinja, Go templates, GitHub Actions `${{ }}` etc. Only exact
  declared/built-in variable names are substituted.
- **Never write outside the target root.** Resolve every rendered path and reject
  anything that escapes it (`..`, absolute paths, drive letters from variables).
- **Generate into a staging folder, then move.** A failure mid-generation must not
  leave a half-written project in the user's chosen folder.
- **Binary files are copied byte-for-byte**, never rendered.
- **Never overwrite existing files silently** — detect conflicts first and let the
  UI ask.
- **Post-create commands never run without the user seeing them first** (templates
  can be shared; they're arbitrary code execution). Run via `ProcessStartInfo`
  with `ArgumentList`, `UseShellExecute = false` — never concatenate a shell string.

## App rules (MVVM + UI) — details in `docs/UI_GUIDE.md` and the `avalonia-mvvm` skill
- ViewModels never reference `Views`, `Window`, `TopLevel`, `Application` or Avalonia
  controls; dialogs/pickers/theme/status go through interfaces in the Sdk's `Services/`
  (`IDialogService`, `IThemeService`, `IStatusService`). Code-behind = `InitializeComponent()` only.
- Compiled bindings everywhere (`x:DataType` on every view). XAML namespaces use `using:`
  (`xmlns:c="using:DevHub.Controls"`): `clr-namespace:` without `;assembly=` can't see the Sdk.
- All disk/process work is `async`; never `.Result`/`.Wait()` on the UI thread.
- Every `event`/`+=` subscription has a matching `-=` on the paired lifecycle
  hook; use `event` rather than public delegate fields.
- **No hex colors, pixel sizes or font names in views**: use the tokens in
  `Themes/Tokens/` (in DevHub.Sdk) via `DynamicResource` (`Brush.*`, `Size.*`, `Spacing.*`, `Padding.*`,
  `Radius.*`) and the text classes (`header`, `secondary`, `small`, `code`). Match the
  token type to the property (a number in `Padding` compiles but crashes at runtime).
- Use the components in the Sdk's `Controls/` (`ToolWindowFrame`, `ActionButton`, `EmptyState`,
  `SettingsRow`, …) before building new ones.
- User operations are **actions** (`Actions/`: `AppAction` + an `IActionProvider`), so
  they appear in menus, Find Action (Ctrl+Shift+A) and shortcuts. New screens are
  **documents** (editor tabs), **tool windows** or **dialogs**; follow the recipes in
  `docs/UI_GUIDE.md` §5.
- Settings are saved with `store.Update(s => s.Section = …)` (never a stale
  `AppSettings` copy). Run experiments with `DEVHUB_DATA_DIR` set to a scratch folder.
- Accessible names: list-item ViewModels override `ToString()`; icon-only/composite
  buttons get `AutomationProperties.Name`.

## Git client rules (details in `docs/GIT_CLIENT_PLAN.md`)
- Run git **only through `GitRunner`** (`plugins/DevHub.Git.Core`): `ArgumentList`, no shell string,
  `GIT_TERMINAL_PROMPT=0`; read-only commands pass `ReadOnly = true` (no optional locks).
- Parse **only machine-readable output** (`--porcelain=v2`, `-z`, explicit `--format`).
- **One repository per tab, one `GitRepositoryClient` per repository**; commands always
  name their repository. Git actions act on `DocumentManager.ActiveRepository`.
- Anything that **changes** a repository happens only on a user action; destructive ones
  (discard, reset, delete unmerged branch, force push) need a confirmation saying what is lost.
- Every command shows up in the Git Console (the runner logs it automatically).
- Credentials come only from Git Credential Manager: never ask for, store or log a password or token
  (borrow it per API call with `credential fill`, run as `Sensitive`).
- Core git tests use real git in temp repos (`TempGitRepo`, `[GitFact]` from `tests/DevHub.TestKit`); UI checks use
  scratch repositories, never the user's own.

## Natural-language commands (roadmap M10 — design rules from day one)
A local, user-trained model turns text into a structured tool call; it's a
learning project, so simplicity and inspectability beat capability.
- **The model only proposes, it never acts.** Output is a `DevHubCommand`
  (tool name + arguments) that goes through `CommandValidator` and a confirmation
  screen, then the *same* Core services the UI uses. No AI-only code paths.
- **One seam:** `ICommandInterpreter` in Core. Rule-based, ML.NET and fine-tuned
  LLM interpreters are interchangeable implementations, all scored by the same
  evaluation harness and test set.
- **Keep Core free of ML dependencies.** ML.NET/ONNX packages go in their own
  project(s) (e.g. `DevHub.Nlp`, `DevHub.Training`); Python training code lives in
  `training/`, never referenced by the app. Model files are build artifacts —
  don't commit large binaries.
- **Everything local.** No cloud APIs, no telemetry; the usage log is opt-in and
  stays on disk.
- **Design the rest of the app so this slots in cleanly:** keep generation,
  stamping and import as Core services with plain argument objects that a
  `DevHubCommand` can map to — don't bury that logic in ViewModels.
- When helping with this milestone, **teach**: explain dataset design,
  train/test splits, metrics, overfitting, and (Stage 2) tokenization, LoRA and
  quantization as they come up.

## Working with the user
The user is learning as they build this app. They're experienced in C# (Unity
background) but may be new to Avalonia/XAML/MVVM and desktop app architecture.
- Explain **why** a design choice is made, briefly, the first time a concept
  appears (DataContext, bindings, DI, source generators, staging dirs, ...). Unity
  analogies help (e.g. ViewModel ≈ a plain C# model a MonoBehaviour would read).
- Keep steps small and runnable — each step ends with something that builds.
- **Claude writes all the code** (the user's choice): implement, build, test, then
  explain the key parts. Ask about requirements and design, not about who writes it.
- Keep `docs/ROADMAP.md` checkboxes current when a task is completed.
- Use `/next-step` to pick up where the last session left off.

## Scope decisions (don't change without asking)
- Templates are **folder + `template.json` manifest** on disk (not a single-file
  bundle). Sharing/zip export is a later roadmap item.
- Template library lives in `%APPDATA%/DevHub/` (resolved via
  `Environment.SpecialFolder.ApplicationData`); a settings option may override it.
- No conditional/loop logic in templates for v1 — plain substitution only.
  Conditionals are a listed future item; don't add a full template language.
- Windows first, but keep Core path-agnostic (`Path.Combine`, no hard-coded `\`).
