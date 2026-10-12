# DevHub

A Windows desktop workbench for developers, built with .NET 10 and Avalonia 12 in a
JetBrains-style UI (Rider "New UI" look, Dark and Light themes). It has:

- **Projects**: pick folders, DevHub finds the git repositories in them, detects each
  one's language (incl. Unity and Tauri), and opens it with the right tool.
- **Git client**: Fork-style, one tab per repository. Covers status, staging (per hunk and
  per line), commit, unified and side-by-side diffs, branches, the commit graph, fetch /
  pull / push, merge / rebase / conflicts, stashes, tags, blame and submodules.
- **GitHub**: sign in through Git Credential Manager, publish to GitHub, and clone your
  repositories.
- **Templates**: New Project from templates (built-in or your own): a base command (`dotnet new`,
  `cargo new`…), your files on top, rearranged your way in an editable preview, then post-create commands.

**Every feature is a plugin** on a bare shell. Each developer turns on only what they need, e.g. just
Projects + Git for a Git client, and installs more from the marketplace (Settings → Plugins). The four
above come with DevHub; all plugins, these included, are developed in
[Naurid/DevHub-Plugins](https://github.com/Naurid/DevHub-Plugins) against the Sdk on nuget.org
(`DevHub.Sdk`, `DevHub.Sdk.Core`). This repository is the shell, the Sdk and the docs: the design is in
[`docs/PLUGINS.md`](docs/PLUGINS.md), progress in [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Solution layout

```
DevHub.sln
Directory.Build.props        shared project settings (net10.0, nullable, implicit usings)

src/
  DevHub.Sdk.Core/           the base, no UI: settings store (shell sections + one section per
                             plugin), fuzzy matching, formatting helpers
  DevHub.Sdk/                the UI base every screen builds on: base classes for ViewModels,
                             editor tabs, tool windows, dialogs and settings pages; the action
                             system; service interfaces; the design system (tokens, controls, icons)
  DevHub.App/                the shell only (main window, tabs, tool windows, menus, Settings, Find
                             Action, finding and loading plugins)

tests/
  DevHub.Sdk.Core.Tests/
  DevHub.TestPlugin/         a small plugin (tool window + action) to check the shell with; never shipped

.github/                     CI and security checks, CodeQL, Dependabot, the release workflow
build/Get-Version.ps1        DevHub's version from git (docs/RELEASING.md)
build/Pack-Sdk.ps1           the Sdk as NuGet packages for plugins (nuget.org on release, or a local feed)
build/Get-BundledPlugins.ps1 the bundled plugins (build/bundled-plugins.json) from the marketplace index
tools/plugin-repo/           starter for the marketplace plugins repository (Naurid/DevHub-Plugins)
docs/                        design docs and guides (see below)
```

## Getting DevHub

Releases are on GitHub (Naurid/DevHub → Releases): `DevHub-win.msi` or `DevHub-win-Setup.exe`
installs it for your Windows user, no admin rights needed. An installed DevHub offers new versions
itself ("Update and restart" in the status bar). More plugins: Settings → Plugins → Marketplace.
How releases are made: [`docs/RELEASING.md`](docs/RELEASING.md).

What each project may reference:

```
DevHub.App ──────► DevHub.Sdk ──► DevHub.Sdk.Core          (this repository)
                       ▲  (NuGet)
DevHub.Projects ───────┤ + DevHub.Projects.Core             (DevHub-Plugins)
DevHub.Git ────────────┤ + DevHub.Git.Core
DevHub.GitHub ─────────┤ + DevHub.GitHub.Core + DevHub.Git   (GitHub requires Git)
DevHub.Templates ──────┘ + DevHub.Templates.Core
```

`DevHub.Sdk` and `DevHub.Sdk.Core` never know about a plugin, nothing references `DevHub.App`
(DevHub loads plugins from plugin folders at startup), and Projects and Git never reference each
other (they link through small interfaces in the Sdk).

## Build, test, run

Requirements: the .NET SDK 10.0.x (Avalonia 12's source generators need it), and PowerShell 7
(`pwsh`) for the build scripts. A Debug build fetches the bundled plugins once (from the
marketplace index) into its output, so `dotnet run` has them.

```
dotnet build
dotnet test
dotnet run --project src/DevHub.App
```

After changing the shell or the Sdk, `pwsh tools/plugin-matrix.ps1` starts the Debug build with
every plugin alone, with none and with all (the bundled ones and `tests/DevHub.TestPlugin`), and
reports what loaded. Plugins are built and tested in DevHub-Plugins.

## Settings

Settings live in `%APPDATA%\DevHub\settings.json`. The shell's own sections (window layout,
tabs, appearance) are top-level fields. Each plugin has its own section under
`"plugins"`, keyed by plugin id:

```jsonc
{
  "appearance": { "theme": "dark", "accent": "#3574F0", "compact": false },
  "plugins": {
    "devhub.projects": { "repositoryFolders": ["C:\\Projects"], "languageTools": {} },
    "devhub.git":      { "changesAsTree": true, "diffSideBySide": false }
  }
}
```

Which plugins load is the shell field `"enabledPlugins": ["devhub.projects", "devhub.git"]`
(a plugin's requirements are switched on with it). On the first start, when it's missing, DevHub
asks with a picker (presets "Git client", "Project templates", "Everything", or tick exactly what
you want); later, change it in **Settings → Plugins**. Plugins load at startup, so changes apply
after a restart (the page offers Restart Now; also File → Restart DevHub). The plugins today:
`devhub.projects`, `devhub.git`, `devhub.github` (requires Git) and `devhub.templates`
(requires Projects). Projects and Git are independent: each works alone (Git has Open
Repository… and Clone), and with both on they link up: double-click a project to open its
repository tab, and Git's Open in Editor uses Projects' Open With.

Settings files from before the split (with top-level `git`, `languageTools` and
`repositoryFolders`) are moved into these sections automatically.

To try things without touching your real settings, set `DEVHUB_DATA_DIR` to another folder
before starting the app.

## Docs

| File | What it covers |
|---|---|
| [`docs/PLUGINS.md`](docs/PLUGINS.md) | Plugins: how they load, the contract, extension points, linking plugins, recipe for a new one, the marketplace |
| [`docs/RELEASING.md`](docs/RELEASING.md) | Versions, CI and security checks, releases (.msi), in-app updates, GitHub settings |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Milestones and what's next |
| [`docs/UI_GUIDE.md`](docs/UI_GUIDE.md) | UI rules, design tokens, components and recipes |
| [`docs/ACTIONS_AND_FUZZY_MATCHING.md`](docs/ACTIONS_AND_FUZZY_MATCHING.md) | Actions, menus, shortcuts, Find Action |
| [`GIT_CLIENT_PLAN.md`](https://github.com/Naurid/DevHub-Plugins/blob/main/docs/GIT_CLIENT_PLAN.md) (DevHub-Plugins) | The Git client's design and rules |
| [`TEMPLATE_FORMAT.md`](https://github.com/Naurid/DevHub-Plugins/blob/main/docs/TEMPLATE_FORMAT.md) (DevHub-Plugins) | The template format (spec for the Templates plugin) |
| [`CLAUDE.md`](CLAUDE.md) | Working rules for the codebase (also read by Claude Code) |
