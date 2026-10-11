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
- **Templates** (planned): project and file templates with variables, generated into a
  folder, with optional post-create commands.

DevHub is being restructured so that **every feature is a plugin** on a bare shell. Each
developer turns on only what they need, e.g. just Projects + Git for a Git client. The
design is in [`docs/PLUGINS.md`](docs/PLUGINS.md) and progress is in
[`docs/ROADMAP.md`](docs/ROADMAP.md) (milestone P).

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
  DevHub.App/                the shell (main window, tabs, tool windows, menus, Settings, Find
                             Action). Right now it still contains the UI of Projects, Git and
                             GitHub; those move into plugin projects in P4.

plugins/                     one folder per plugin part; logic lives in X.Core (no UI, tested)
  DevHub.Projects.Core/      repository scanning, language detection, "Open With" tools
  DevHub.Git.Core/           running git, parsing its output, diff / graph layout
  DevHub.GitHub.Core/        Git Credential Manager + GitHub API (requires Git)

tests/
  DevHub.TestKit/            shared helpers: TempGitRepo (real git in a temp folder), [GitFact]
  DevHub.Sdk.Core.Tests/
  DevHub.Projects.Core.Tests/
  DevHub.Git.Core.Tests/     uses real git; skipped automatically when git isn't installed
  DevHub.GitHub.Core.Tests/

docs/                        design docs and guides (see below)
```

What each project may reference:

```
DevHub.App ──► DevHub.Sdk ──► DevHub.Sdk.Core
     │
     └──────► DevHub.Projects.Core ──► DevHub.Sdk.Core
     └──────► DevHub.Git.Core ───────► DevHub.Sdk.Core
     └──────► DevHub.GitHub.Core ────► DevHub.Git.Core, DevHub.Sdk.Core
```

`DevHub.Sdk` and `DevHub.Sdk.Core` never know about a plugin, and nothing references
`DevHub.App`.

## Build, test, run

Requirements: the .NET SDK 10.0.x (Avalonia 12's source generators need it), and git ≥ 2.30
for the Git features and their tests.

```
dotnet build
dotnet test
dotnet run --project src/DevHub.App
```

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
(missing = all of them; a plugin's requirements are switched on with it). Until the Plugins page
arrives (P5) you can edit it by hand; changes apply on the next start. The plugins today:
`devhub.projects`, `devhub.git` (requires Projects), `devhub.github` (requires Git) and
`devhub.templates` (requires Projects).

Settings files from before the split (with top-level `git`, `languageTools` and
`repositoryFolders`) are moved into these sections automatically.

To try things without touching your real settings, set `DEVHUB_DATA_DIR` to another folder
before starting the app.

## Docs

| File | What it covers |
|---|---|
| [`docs/PLUGINS.md`](docs/PLUGINS.md) | Plugin architecture: the contract, extension points, migration phases |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Milestones and what's next |
| [`docs/UI_GUIDE.md`](docs/UI_GUIDE.md) | UI rules, design tokens, components and recipes |
| [`docs/ACTIONS_AND_FUZZY_MATCHING.md`](docs/ACTIONS_AND_FUZZY_MATCHING.md) | Actions, menus, shortcuts, Find Action |
| [`docs/GIT_CLIENT_PLAN.md`](docs/GIT_CLIENT_PLAN.md) | The Git client's design and rules |
| [`docs/TEMPLATE_FORMAT.md`](docs/TEMPLATE_FORMAT.md) | The template format (spec for the Templates plugin) |
| [`CLAUDE.md`](CLAUDE.md) | Working rules for the codebase (also read by Claude Code) |
