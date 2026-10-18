# DevHub Roadmap

Check boxes as tasks land. Each task should end in something that builds (and,
for Core, has tests). `/next-step` reads this file to decide what's next.

**Two repositories.** [DevHub] = Naurid/DevHub (`C:\P\DevHub\DevHub`): the shell, the Sdk, the docs and this
roadmap. [Plugins] = Naurid/DevHub-Plugins (`C:\P\DevHub-Plugins`): every plugin. Each milestone says where its
work happens; the plugin milestones done before PM4 were built in DevHub and moved with their history.

## Repository workbench [done; Projects plugin → Plugins]
A parallel track turning DevHub into a git/project workbench (alongside the template
milestones below).
- [x] Repositories page: designate folders, scan for git repos, list them
- [x] Per-repo language detection (marker files; incl. Unity and Tauri)
- [x] "Open with the right tool" + per-language tool map persisted to `%APPDATA%/DevHub/settings.json`
- [x] UI-0: JetBrains-style front end (see below) — design system + IDE shell,
      built before the Git client so the Git UI is mostly composition. Rules and recipes:
      `docs/UI_GUIDE.md`.
- [x] Integrated Git client (in-app, "Fork"-like): milestones G0–G9 below.

## UI-0 — JetBrains-style front end (Rider New UI look) [DevHub]
Dark + Light, configurable accent (default #3574F0), Compact mode, Inter UI + JetBrains
Mono code. Layers: Theme → Components → Actions → Shell → Features. The user writes,
Claude reviews. Rules land in `docs/UI_GUIDE.md` (no hex/sizes/fonts in views).
- [x] 0. Spikes: `Svg.Controls.Skia.Avalonia` 12.0.0.17 targets Avalonia 12; icons at
      intellij-community `platform/icons/src/expui/` (`x.svg` / `x_dark.svg`, stripe icons
      `x@20x20.svg`); JetBrains Mono v2.304 (OFL)
- [x] 1. Tokens: `Themes/Tokens/` Colors (Dark+Light), Sizes (+Compact), Fonts; `DevHubTheme.axaml`
- [x] 2. Fluent key mapping + ControlThemes + debug UI Gallery
- [x] 3. `Icon` control + first ~20 vendored icons (+ Apache-2.0 NOTICE)
- [x] 4. Components: ToolWindowFrame, Toolbar, ActionButton, SearchField, EmptyState, SettingsRow, Badge
- [x] 5. Action system (registry, providers, keymap, MenuBuilder) + Core `FuzzyMatcher` (tested)
- [x] 6. Merged title bar + main toolbar + status bar
- [x] 7. Tool-window framework (fixed slots, stripes, persisted layout); Repositories → tree tool window
- [x] 8. Editor area (document tabs) + Welcome view; Generate/Import as documents; drop sidebar
- [x] 9. Dialog framework + Settings dialog (Ctrl+Alt+S): Open With + Appearance pages, persisted
- [x] 10. Find Action popup (Ctrl+Shift+A)
- [x] 11. `docs/UI_GUIDE.md` (rules + recipes), CLAUDE.md App-rule update

## G — Integrated Git client (Fork / JetBrains-style) [done; Git plugin → Plugins]
Plan, decisions and rules: `docs/GIT_CLIENT_PLAN.md`. Confirmed 2026-10-09: **git CLI**
(machine-readable output), **Fork + JetBrains functionality with JetBrains styling**, and
**one editor tab per repository** (several repos side by side). First release = G0–G4.
- [x] G0. Foundation + repository tabs: `GitExecutable`, `GitRunner`, `GitCommandLog`
      (Core, tested with real git in temp repos); multi-instance documents (id factory,
      restore); repository tab with empty sidebar + header; open from Repositories; title-bar
      branch widget (read-only); Git Console tool window
- [x] G1. Status: porcelain v2 parser (tested); watcher refresh; Local Changes view
      (read-only), change count in the sidebar, dirty dot on the tab; Projects (then: Repositories) tool window
      scans saved folders automatically (at startup and after adding a folder)
- [x] G2. Diff: unified-diff parser (tested); diff pane in Local Changes; "Open in Editor" (F4) in the language's external editor; diff in its own tab
- [x] G3. Stage / unstage (double-click, Space, Stage All / Unstage All) / discard (confirmed) /
      commit (Ctrl+K, Ctrl+Enter) / amend; failing hooks shown in the commit box
- [x] G3.1 Multi-select in the file lists; per-hunk / per-line staging (`git apply --cached`);
      hunk / line discard (confirmed)
- [x] G4. Branches: sidebar Branches/Remotes, checkout (dirty-tree handling), create, rename,
      delete; title-bar branch popup
- [x] G5. All Commits: log parser + graph layout (tested), commit list, details + diffs;
      search, "Load more" (1,000 at a time), reload when HEAD or a branch moves
- [x] G6. Remotes: fetch / pull / push with progress + cancel; auth errors explained;
      Commit and Push (Ctrl+Alt+K), Force Push (--force-with-lease, confirmed)
- [x] G7. Merge / rebase / conflicts: state, continue / abort, conflict list, merge tool;
      Merge into Current / Rebase onto from the branch menu, Accept Yours / Theirs, Skip (rebase)
- [x] G8. Stashes (stash / apply / pop / drop, files + diffs), Tags (new / push / delete local + remote),
      cherry-pick / revert from All Commits, Blame tab, Submodules (update, open as a tab)
- [x] G9. Remotes & GitHub: Manage Remotes (add / edit / remove), GitHub account via Git Credential
      Manager (Settings → GitHub), Publish to GitHub, Clone Repository (your GitHub repositories or a URL)
- [x] G9.1 Fork-style sidebar tree (Branches / Remotes → origin → folders → branches); Local Changes
      in folders (tree / flat switch, saved)
- [x] G9.2 Side-by-side diff (layout in Core, tested): one saved switch for every diff; hunks staged in
      both views, single lines in unified. Fetch from an empty remote says so instead of looking like a no-op

## P — Plugin architecture [DevHub]
Every feature becomes a plugin on a bare shell; each dev enables what they need (Settings →
Plugins, first-run presets, restart to apply). Design and decisions: `docs/PLUGINS.md`.
Templates (M1–M9) are then built as the `DevHub.Templates(.Core)` plugin.
- [x] P1. Core split: `Directory.Build.props`; Sdk.Core, Projects.Core (+ Repositories,
      LanguageToolChoice), Git.Core, GitHub.Core; tests split + TestKit; settings sections + migration (tested)
- [x] P2. `DevHub.Sdk`: base classes, Controls, Themes, action contracts, service interfaces; ViewLocator,
      Icon and avares URIs work across assemblies (XAML uses `using:` namespaces)
- [x] P3. Extension points + `IPlugin` / `PluginHost` / `PluginResolver` (tested); menus from action
      metadata, settings-page list, title/status-bar widgets, Welcome placement, tool-window order;
      no `ActiveRepository` in the shell; the four features are `IPlugin`s (still inside DevHub.App),
      `enabledPlugins` in settings decides what loads; Projects and Git independent of each
      other (linked through `DevHub.Integration`: `IProjectOpener`, `IProjectFileEditor`, `IProjectFolders`;
      Git → Open Repository…); GitHub → Git the only hard link, GitHub optional for Git
- [x] P4. Projects, Git, GitHub and Templates in their own plugin assemblies (`plugins/DevHub.*`), each
      with its plugin class and ids; Git's tokens as plugin resources; `IDocumentService` (Sdk) instead of
      the shell's DocumentManager; Git's `IRemoteHost` implemented by GitHub (Publish, avatar, Clone list);
      the app ships the plugins without using their types (`Aliases="plugins"`)
- [x] P5. Settings → Plugins page (switches, requirements kept consistent, what changes after a restart,
      why a plugin couldn't load, Restart Now), first-run picker (presets declared by the plugins +
      checklist), File → Restart DevHub; selection rules in Sdk.Core (`PluginSelection`, tested)
- [x] P6. Docs: `PLUGINS.md` as the reference + "new plugin" recipe; `tools/plugin-matrix.ps1` (start
      DevHub with every plugin alone / none / all); tabs of a switched-off plugin are kept in the saved
      layout and come back with it; CLAUDE.md, UI_GUIDE, README, skills

## M0 — Skeleton [DevHub]
- [x] `git init`, .NET `.gitignore`, `DevHub.sln`
- [x] `src/DevHub.Core` (net10.0 class library)
- [x] `src/DevHub.App` (Avalonia 12 app, Fluent theme, compiled bindings on by default)
- [x] `tests/DevHub.Core.Tests` (xUnit) referencing Core
- [x] DI container in `App.axaml.cs`, `ViewLocator`, `MainWindow` + `MainWindowViewModel`
- [x] `dotnet build` and `dotnet test` green; the app opens a dark app-shell window
      (sidebar nav: Templates / Generate / Import / Settings, placeholder pages)

## M1 — Template model & libraries (Core) [Templates plugin → Plugins]
M1–M9 build the **Templates plugin** to the spec in `docs/TEMPLATE_FORMAT.md` (decided
2026-10-11): project + item templates, base command + content + steps + post-create, language +
categories, several libraries + read-only built-ins, staging → editable preview → place. The
engine goes in a new `plugins/DevHub.Templates.Core` (+ `tests/DevHub.Templates.Core.Tests`),
the screens in `plugins/DevHub.Templates`. "Core" below means `DevHub.Templates.Core`.
- [x] Manifest model (kind, language, categories, variables, rootFolder, baseCommand, steps,
      postCreate) + JSON load/save: unknown fields round-trip, bad `schemaVersion` refused
- [x] Manifest validation (ids, variable names/types/choices, `when` names a bool, step fields, kind rules)
- [x] Libraries: personal (`%APPDATA%/DevHub/templates`), extra folders, read-only built-in; list / load /
      save / delete / duplicate; same id in two libraries
- [x] Tests for all of the above (57: serializer, validator, store, libraries)

## M2 — Placeholder renderer (Core) [Templates plugin → Plugins]
- [x] Tokenizer: `{{Name}}`, `{{Name:transform}}`, `\{{` escape (one pass: values never re-scanned)
- [x] Transforms: pascal/camel/kebab/snake/upper/lower (word split on separators + case changes)
- [x] Built-ins: Year, Date, Author, Guid1..9 (stable per generation)
- [x] Unknown tokens pass through verbatim (Vue, Jinja, GH Actions, Handlebars, Helm samples)
- [x] Path segment rendering + validation (Windows names); commands (+ `when`), step paths, rootFolder;
      entered values checked (required / pattern / choice / bool) — 61 tests

## M3 — Generation pipeline (Core) [Templates plugin → Plugins]
- [x] Command runner: ArgumentList, streamed output, cancellation, exit codes, `.cmd` shims (npm/npx), tool aliases (`@unity`)
- [x] Staging + base command (`runIn` root / parent); overlay of `content/` (binary detection, encoding/BOM kept)
- [x] Steps engine (createFolder / move / rename / delete, globs, nothing outside the root)
- [x] Result tree model for the preview; preview edits → applied + convertible to `steps` ("Save to template")
- [x] Conflict check, place into target; item templates applied into an existing folder (incl. moving existing files)
- [x] Post-create commands; the "commands that will run" list for the confirmation; tests in temp folders

## M4 — Browse + generate UI [Templates plugin → Plugins]
- [x] Template browser: group by language or category, filter by both + library, search; project / item
- [x] Variable form from the manifest (string / bool / choice, validation messages); target folder picker; Raw
- [x] Confirmation (every command, rendered) → progress → editable preview tree (rename / move to / delete /
      new folder, Save to template) → place → post-create with live output
- [x] First built-ins to try it with: Console App (.NET, `dotnet new` + steps + post-create), Empty Project (folders only)

## M5 — Template editor UI [Templates plugin → Plugins]
- [x] Templates tab (library manager: New Template…, Edit, Use in New Project, Duplicate, Delete) + one editor tab per template:
      General, Variables, Base Command, Steps, Post-create (Save Ctrl+S, Revert, asks before losing changes)
- [x] Files: tree of `content/` (new file / folder, rename, delete), files edited in your external editor (Open With)
- [x] template.json section (edit the raw manifest; read-only for built-ins); Duplicate to Personal; Explorer / VS Code
- [ ] In-app text editor for content files (chose external editors for now)

## M6 — Item templates [Templates plugin → Plugins]
- [x] Add Files tab (Details → Preview → Done), opened from Projects (right-click), a repository tab, the Templates tab, File menu
      (hook `IAddFilesFromTemplate` in the Sdk); conflicts with an Overwrite choice each
- [x] Moves / deletes of existing files spelled out in the preview; only what changes shown (or everything)
- [x] Namespace inference (nearest `.csproj` RootNamespace or name, `.asmdef` rootNamespace, Unity project; + folders)
- [x] Starter items: C# class, interface, Unity MonoBehaviour, ScriptableObject, Python module
- [x] Templates tool window (left stripe, Alt+2): the list at hand; double-click / Use starts New Project or Add Files, Edit, New Template

## M7 — Import from an existing folder [Templates plugin → Plugins]
- [x] Ignore rules, gitignore-style (name anywhere, name/ folders, /path from the top, globs) (Core, tested)
- [x] Import tab: preview tree with left-out items greyed out and placeholders per file
- [x] Literal → variable replacement; other spellings of a multi-word literal become transforms; an existing {{Var}} is escaped
- [x] Save as a new template (project or item) in a chosen library; it opens in the editor

## M8 — Built-in templates [Templates plugin → Plugins]
- [x] .NET (console, classlib, solution with tests, add project to solution, Avalonia, xUnit), Unity (`@unity`, newest Hub editor)
- [x] Web (Vite React/Vue/Svelte TS, Tauri), Rust (cargo bin/lib), Python (src layout + .venv), Go (go mod)
- [x] Each checked against the real tool when installed (npm ones with DEVHUB_NETWORK_TESTS=1; Unity by its command)

## M9 — Polish [Templates plugin → Plugins]
- [x] Settings → Templates (author name, personal library location, extra libraries, default project folder,
      project folder rules per language and/or category)
- [x] Recent projects on New Project's start page (Open / Show)
- [x] Preview: drag and drop to move files and folders (onto a folder, a file = its folder, empty space = the top), Undo (Ctrl+Z)
- [x] Zip export/import of a template (Templates tab; zip-slip refused, free id when taken)
- [x] App icon, publish profile (`dotnet publish src/DevHub.App -p:PublishProfile=win-x64` → artifacts/publish/win-x64)

## R — Releases, updates, marketplace (docs/RELEASING.md, docs/PLUGINS.md §9) [DevHub + Plugins]
- [x] R1 CI on every push / PR (build with warnings as errors, tests, vulnerable packages, SBOM, dependency
      review), CodeQL, Dependabot; version from git (`build/Get-Version.ps1`: `vN` major tags, +1 minor per push on main)
- [x] R2 Release workflow: on main / `vN` → Velopack: Setup.exe, .msi, portable zip, full + delta update
      packages, GitHub Release `release/<version>` (+ `DevHub-Sdk-<version>.zip` for plugins); signing ready (secrets)
- [x] R3 In-app updates: quiet check after start, "Update and restart" in the status bar, Settings → Updates,
      Help → Check for Updates (`DEVHUB_UPDATE_SOURCE` to rehearse locally)
- [x] R4 Marketplace in DevHub: index sources, install / update / remove after a restart, SHA-256 + zip-slip
      checks, plugin API version, plugins loaded from `%AppData%\DevHub\plugins`
- [x] R5 Plugins repo starter (`tools/plugin-repo/`): HelloWorld sample, per-plugin versions (`<id>/vN`),
      publish workflow (release per plugin version + `index.json`)
- [x] You: the one-time GitHub settings, first `v1` tag, Naurid/DevHub-Plugins created (from the
      starter for now; PM below moves every plugin into it)
- [ ] Code signing (Azure Trusted Signing or a certificate) once SmartScreen warnings matter

## PM — Every plugin in DevHub-Plugins; the Sdk on NuGet (before B, D, CI) [DevHub + Plugins]
Decided 2026-10-12: DevHub keeps only the shell and the Sdk; **every plugin** (Projects, Git, GitHub,
Templates, and the new Board, Diagrams, CI) is developed, versioned and released in
**Naurid/DevHub-Plugins**. The first four are **bundled** in DevHub's installer (offline first run, presets
as today) and then **update through the marketplace**. The Sdk goes to **nuget.org**. The plugins move
**with their git history**. Each step leaves both repos building, tested and releasable.
- [x] PM1. Sdk as NuGet packages: `DevHub.Sdk` + `DevHub.Sdk.Core` packed with DevHub's version (and
      `PluginApi.Version`), pushed to nuget.org by `release.yml` (Trusted Publishing; you: the nuget.org policy + a `NUGET_USER` variable); the
      plugins repo uses a `PackageReference` instead of the Sdk zip; a local feed folder
      (`build/Pack-Sdk.ps1` → `%LocalAppData%\DevHub-local-feed`) to try Sdk changes before releasing them
- [x] PM2. Dependencies between marketplace plugins:
  - [x] PM2a. Installing one installs what it `requires` (listed first, all or nothing; a requirement
        nobody offers, for another plugin API or in a circle stops it); removing one others need asks
        first and removes them too (`InstallPlanner`, tested); loading in `requires` order (`PluginResolver`)
  - [x] PM2b. A package leaves out the DLLs of the plugins it requires (`Pack-Plugin.ps1`); marketplace
        folders load requirements first, so one copy loads, the required plugin's own; the Sdk's assembly
        version is the plugin API version (a plugin built against a newer Sdk loads in an older DevHub)
  - [x] PM2c. Bundled plugins are plugin folders in `plugins\` next to the app; an updated copy in
        `%AppData%\DevHub\plugins` wins while it's newer (`PluginFolders`, tested), only the chosen folder
        loads; Marketplace page: Bundled / Update to / Back to / Remove Old Copy
- [x] PM3. Plugin development against an installed DevHub: `DEVHUB_PLUGIN_PATHS` (load plugins from build
      output folders; they win and are switched on for the run), a generated `launchSettings.json` in the
      plugins repo (F5 in Rider debugs a plugin inside DevHub, own data folder), `build/Test-Plugins.ps1`
      there (the plugin matrix against those folders; DevHub's `tools/plugin-matrix.ps1` changes in PM5)
- [x] PM4. Move with history (`git filter-repo`): `plugins/`, their tests, `tests/DevHub.TestKit`, the
      template-engine skill, `GIT_CLIENT_PLAN.md`, `TEMPLATE_FORMAT.md` → merged into DevHub-Plugins next to
      HelloWorld, grouped (`plugins/Git/DevHub.Git` + `DevHub.Git.Core`, tests in `tests/`, `DevHub-Plugins.sln`);
      Sdk project references → the NuGet packages (`Directory.Build.props`: plugin / .Core / test kinds); their
      CI there runs the moved tests (Windows: real git / dotnet / cargo / go); per-plugin versions (`<id>/vN`,
      counted over `plugins/<Name>/`); `"publish": false` until PM5 (first release tagged `v1` then); Templates
      finds BuiltInTemplates next to its own DLL and its package carries them; the local Sdk feed is
      `%LocalAppData%\DevHub-local-feed`. DevHub still has its copies until PM5.
- [x] PM5. DevHub without plugins: `plugins/`, the plugin tests and the `Aliases="plugins"` references
      removed; `release.yml` bundles the latest Projects / Git / GitHub / Templates packages from the index
      (`build/Get-BundledPlugins.ps1` + `build/bundled-plugins.json`, SHA-256 checked, all or nothing; a Debug
      build fetches them once); first-run presets still offline; the shell is checked with a small test plugin
      (`tests/DevHub.TestPlugin`, in `tools/plugin-matrix.ps1`). Release order: DevHub with the pinned Sdk →
      the plugins' `v1` in DevHub-Plugins → DevHub with PM5
- [x] PM6. Shared pieces for the new plugins, in the Sdk: hooks for commit-message suggestions,
      commit/branch decorations (CI marks), branch/commit events, a GitHub API client lent by the GitHub
      plugin (`IGitHubApi`, no token handed out); UI blocks: a drag-and-drop board/list panel (from
      `TreeDragDrop`), a Markdown viewer, simple charts (line, bar, pie), a people picker. Plugins in the
      same repo may reference each other's `.Core` for a declared hard dependency (Board.Git → Git)
  - [x] PM6a (DevHub). The contracts (`Integration/GitIntegration.cs`: `IRepositoryEvents`,
        `ICommitMessageSuggester`, `IGitDecorator`; `GitHubIntegration.cs`: `IGitHubApi`); Sdk.Core `Markdown`,
        `Charts` (`ChartData`, `ChartMath`), `People` (`Person`, `PeopleSearch`), `Collections/ListMoves` (all
        tested); controls `MarkdownView`, `LineChart` / `BarChart` / `PieChart` (`Brush.Chart.1…6`),
        `ItemsDragDrop`, `TreeDragDrop` (moved from Templates), `PeoplePicker`; all in the UI Gallery; docs
  - [x] PM6b (DevHub-Plugins). Git offers `IRepositoryEvents` (the client's `OperationCompleted`, tested; branch
        changes from each tab's status refresh) and uses `ICommitMessageSuggester` (Suggested… / Use above the
        commit box) and `IGitDecorator` (marks in All Commits and the branch tree); GitHub offers `IGitHubApi`
        (`SendRawAsync`: api.github.com only, tested); Templates uses the Sdk's `TreeDragDrop`. Checked end to end
        with DevHub's test plugin (`IntegrationProbe`). Needs the Sdk with PM6a: DevHub release first
- [x] PM7. Docs: DevHub's `CLAUDE.md`, `README.md`, `PLUGINS.md`, `RELEASING.md`; DevHub-Plugins' README +
      its own `CLAUDE.md` (the rules that moved with the plugins); this roadmap names each milestone's repo

## B — Board: Jira-style agile planning, no server [Plugins]
Decided 2026-10-12/13: a board is plain JSON files, **no server and no git needed**. Storage sits behind
`IBoardStore`: `Board` keeps a board in a folder (`%APPDATA%/DevHub/boards/<key>` or any folder you pick);
`Board.Git` adds a repository's `devhub/board` branch (written with git plumbing, never checked out, the same on
every code branch). One user for now; multi-user (merges, key renumbering) later. Fully modular: `DevHub.Board`
(+ `.Core`, requires nothing), `Board.Scrum`, `Board.Git` (→ Board + Git), `Board.Reports`, `Board.Timeline`.
Code in DevHub-Plugins `plugins/Board*/`; plan in its `docs/BOARD_PLAN.md`.
- [x] B0. Plan + decisions (`docs/BOARD_PLAN.md`): stores (folder; `devhub/board` branch in Board.Git), one JSON
      file per issue with a stable id + `board.json`, keys like `DEV-12`, LexoRank-style ranks, a change log per
      issue (history without git), story points + time tracking, build order
- [x] B1. Core model + folder store (tested): issue types (epic, story, task, bug, sub-task) + custom types; fields
      (summary, Markdown description, status, priority, assignee, reporter, labels, components, fix versions,
      story points, original / remaining estimate + work log, due date, custom fields); links (blocks, relates
      to, duplicates, clones), parent / epic; comments, attachments, history; `IBoardStore` + the folder store
      (compare-and-swap writes, outside changes); operations as plain argument objects
- [x] B2. Workflows (tested): statuses in To Do / In Progress / Done categories, allowed transitions,
      resolutions, a workflow editor. Nothing is fixed: the defaults are a starting preset, every status
      can be renamed, reordered or deleted
- [x] B3. Kanban board with **fully configurable columns**: add, rename, reorder (drag), merge, delete
      columns right on the board (a new column creates its status; deleting one asks where its issues go;
      you can remove every default column and start empty); a column can hold several statuses, statuses on
      no column are listed as "not on the board"; several boards per project (own columns, filter, card
      layout; presets Kanban / Scrum / Bug triage / empty); swimlanes (none, assignee, epic, priority);
      WIP limits, quick filters; drag cards between columns (allowed transitions only) and to rank; New Board
      (where it's kept), recent boards
- [x] B4. Issue view (tab + side panel): every field editable, comments, links, sub-tasks, time tracking (log
      work), history, attachments; Create Issue (C)
- [x] B5. Backlog: ranked list, epics panel, bulk edit, story-point totals
- [x] B6. Search: a JQL-like query language (parser + evaluator in Core, tested), saved filters, results as
      a list or a board
- [x] B7. `Board.Scrum`: sprints (create, start with goal and dates, complete with leftovers moved), sprint
      board, capacity, points per sprint — first usable version with B1–B5
- [x] B8. `Board.Git`: the `devhub/board` branch store (New Board on the branch, Move board…, Push / Pull board);
      branch from an issue (`feature/DEV-12-short-summary`); issue keys in branches and commits linked both
      ways (Development section: branches, commits, pull requests via `IRemoteHost`); smart commits
      (`DEV-12 #done #comment …`) shown before they apply, never silent; the commit box suggests the current
      branch's key
- [x] B9. `Board.Reports`: burndown / burnup, velocity, cumulative flow, control chart, sprint report, epic
      progress; dashboards with gadgets (filter results, pie by field, assigned to me)
- [x] B10. `Board.Timeline`: roadmap (Gantt) of epics with dates and dependencies; releases (fix versions with
      dates, release notes from their issues)
- [x] B11. Import / export: Jira CSV import, CSV / JSON export, GitHub Issues import (through GitHub)
- [x] B12. Actions in Find Action (Go to Issue, My Open Issues, Create Issue, Backlog, Search…). M10 command tools (create issue, move issue, my open issues) are left for M10

## BS — Board for Scrum projects (a course's agile project dossier) [DevHub + Plugins]
Decided 2026-10-17: the Board supports everything a Scrum project dossier needs (backlog quality, sprint planning with
capacity, Definition of Done, three velocity scenarios, KPIs, risks, retrospectives, a scope change) as working tools
used in real time: no simulated clock, no report generation; charts and tables export for the report. Charts became an
Sdk library any plugin uses, with a chart maker for your own data.
- [x] BS1. Acceptance criteria on issues, a Definition of Done per project (Off / Ask / Strict when moving to Done),
      team roles, Business value and Rank by Business value, criteria in Create Issue, search and CSV
- [x] BS2. Sprint capacity from each person's available days and a focus factor (suggested from points per focused
      day); a Retrospectives page (went well, to improve, problems, actions with owners and issues, follow-up)
- [x] BS3. Sdk charts: area, stacked, horizontal bars, combo, scatter, heatmap, gauge; dashed / dotted lines, markers,
      value labels, bands, reference lines, axis titles; `c:Chart Kind`; `IExportService` (PNG, copy image, CSV, copy
      table); drag and drop shows the dragged item
- [x] BS4. Reports: release forecast (pessimistic / neutral / optimistic, a planned ramp-up, delivery dates against a
      target), baselines, scope change, sprint plans, commitment reliability, throughput, capacity and load, lead time,
      WIP, defects; a chart type per report; export
- [x] BS5. Board.Risks: a risk register with a probability × impact matrix, owners, mitigation and contingency
- [x] BS6. Charts plugin: the chart maker (paste a table, any chart type and style, save and export); reports open in it

## L — Languages: French [DevHub + Plugins]
Decided 2026-10-17: English and French, chosen in Settings → Appearance (default: the Windows language), applied at
the next start. The English text is the key (`L.T`, `{l:T …}`), so anything untranslated stays English; each
assembly embeds its `i18n/fr.json`. New boards get French names (statuses, types, priorities) when the app is in
French; existing boards keep theirs.
- [x] L1. Sdk: `L` (Sdk.Core), `{l:T}` (Sdk), the Language setting, French dates and numbers; tools/I18n (wrap, keys,
      check; CI checks every text has its French); the shell in French
- [x] L2. Plugins in French: Board (and Scrum, Reports, Timeline, Risks, Git), Charts, Git, GitHub, Projects, Templates;
      new boards with French names
- [x] L3. Each assembly's translations first for its own text (the same English can differ per plugin)

## RW — Responsive layout [DevHub + Plugins]
Decided 2026-10-18: every screen works from 800 px wide. Side panels fold into a rail and open over the content;
rows of tabs and buttons put what doesn't fit into a « More » menu; headers shrink through container queries.
- [x] RW1. Sdk: `SidePane`, `OverflowPanel`, `Shadow.Overlay`; the title bar (breadcrumb shrinks, toolbar
      overflows), Welcome wraps; heatmap labels end in "…"
- [x] RW2. Plugins: repository tab (sidebar, changes list, diff and sidebar headers), commit details, board header
      (pages, search, Create) and issue panel, backlog epics, reports, risks, releases, chart maker, Templates screens
      and editor lists, project settings

## D — Diagrams: UML, databases, business processes, mood boards (Visual Paradigm's range) [DevHub + Plugins]
Decided 2026-10-12: diagrams **export images** (and diagram text) and **link to the board**. 2026-10-18: the course
PDFs are read (notation rules in DevHub-Plugins' `docs/DIAGRAMS_PLAN.md`); added **Merise MCD → MLD with SQL export**,
**mood boards** and the **link to a project**; then widened to what Visual Paradigm draws (the course is a guideline):
all the usual UML diagrams, crow's-foot ERD, flowcharts, BPMN, mind maps, and formatting on every shape and line.
The canvas is our own Avalonia control (no maintained Avalonia 12 diagram library fits). Every text in English and
French. Plugins: `Diagrams` (+ `.Core`: model, operations, geometry, file format; UI: canvas, tab, export) and kinds
through the `IDiagramKind` extension point: `DiagramsUml`, `DiagramsDatabase`, `DiagramsBusiness`, `MoodBoard`.
Files are `*.devhub-diagram.json`, opened as tabs.
- [x] D0. PDFs read → elements and notation rules per kind; "actions" = activity diagrams; canvas built ourselves
- [x] D1. Core: model, file format (schema version, unknown fields kept), operations + Undo / Redo, geometry
      (orthogonal routing around shapes, snapping, guides, hit tests), rules framework, layered auto layout
- [x] D2. Editor: canvas (pan / zoom, grid, select, move, resize, connect, bends, labels, align, copy / paste,
      minimap), tab per file, palette, properties (fill, line, text colour, size, bold on every shape and line),
      problems, export PNG / SVG / copy image; Sdk: image / text export, pasted images and files
- [x] D3. UML: use case (stereotype actors, extension points, include / extend / generalization), activity
      (guards, fork / join, object nodes, signals, swimlanes), class and object (compartments typed as text lines),
      sequence (lifelines, activations, sync / async / reply / create, fragments), state machine, component,
      deployment, package; the course's rules
- [x] D4. Databases: Merise MCD (entities, associations, cardinalities, identifiers) → generated MLD, crow's-foot
      ERD, data dictionary, SQL export (MySQL, PostgreSQL, SQL Server, SQLite) and CREATE TABLE import (MLD + MCD)
- [x] D5. Business: flowcharts, BPMN (pools, lanes, events with triggers, typed tasks, gateways, message flows,
      data), mind maps (coloured branches, Add Idea, two-sided tree layout)
- [x] D6. Mood boards: pictures (stored beside the file; drop, paste, pick), colour swatches and palette from a
      picture, shapes and frames, link cards, text, freehand pen and eraser
- [x] D7. Text exports: PlantUML, Mermaid, SQL, MLD text, Markdown outlines, CSS colours
- [x] D8. Board link: a shape linked to an issue (status on the shape), "Create Story from This Shape"; an issue
      lists the diagrams linked to it; a board has a Diagrams page for its project
- [x] D9. Project link: diagrams belong to a project (Projects lists them under each project; new ones go to its
      `docs/diagrams/`), through hooks in `Integration/` so Projects, Board and Diagrams stay independent
- [x] D10. Samples from the course in French and English ("Start from" in New Diagram) for every main kind

## CI — CI tracker for GitHub Actions [Plugins]
Decided 2026-10-12: **GitHub Actions** first; providers behind `ICiProvider` (GitLab, Azure DevOps later, as
their own plugins). `DevHub.Ci` (+ `.Core`) needs GitHub on and calls the API through PM6's `IGitHubApi` (the
account stays in Git Credential Manager). Code in DevHub-Plugins; plan in its `docs/CI_PLAN.md`.
- [ ] CI0. Plan: API calls and the token scopes they need (`workflow`), polling with ETags and rate limits,
      which actions are confirmed
- [ ] CI1. Core (tested with recorded API responses): workflows, runs, jobs, steps, logs, artifacts,
      annotations
- [ ] CI2. CI tool window + a tab per repository: runs (workflow, branch, event, status, duration, who) with
      filters, live while something runs; jobs → steps → logs (search, collapsible groups, errors
      highlighted); annotations, artifact download, open in the browser
- [ ] CI3. Re-run (all or failed jobs), cancel, run a workflow (`workflow_dispatch` inputs as a form); each
      confirmed and logged
- [ ] CI4. Status everywhere: the current branch's latest run in the status bar, marks next to commits and
      branches in Git (through a hook), a notification when a watched run ends
- [ ] CI5. Releases: which run built each release; **Redeploy** (re-run a version's release); **Release next
      version**: shows the next version (computed in Core: DevHub's `vN` + commits scheme, or semver tags),
      minor = dispatch the release workflow, major = create and push the `vN` tag, both confirmed; release
      workflow and version scheme set per repository; DevHub's `release.yml` gets `workflow_dispatch` (minor /
      major) so the button works for DevHub itself
- [ ] CI6. Workflow files: list and open `.github/workflows/*.yml`, GitHub's validation errors shown

## X — Multiplatform (Linux, macOS) [DevHub + Plugins]
- [ ] Windows-only bits behind small services: `explorer.exe` (Show in Explorer), `cmd /c` shims and PATHEXT
      (command runner), Git Credential Manager paths, Unity Hub paths (`@unity`), the `py` launcher, title-bar
      caption buttons, `%AppData%` / `%LocalAppData%` locations
- [ ] Tests that run on each OS (the `.cmd` fake tools become per-OS scripts; path assumptions)
- [ ] CI matrix: windows-latest, ubuntu-latest, macos-latest
- [ ] Packages per OS with Velopack (Linux AppImage, macOS .app / .pkg; signing / notarization on macOS)
- [ ] Marketplace packages per OS when a plugin has native parts (`os` field in the index)

## M10 — Natural-language commands (local AI, learning project) [DevHub: command bar, catalog; Plugins: their tools]
Goal: type "new unity project called Timber with git" → a structured tool call →
confirmation screen → same execution path as the UI. See "Natural-language
commands" in `CLAUDE.md` for the design rules. With plugins, the tools come from the plugins
that are switched on: each contributes its commands (an `ICommandTool` extension point in the
Sdk), so the catalog only offers what's loaded.

### Stage 0 — plumbing (no ML yet)
- [ ] Tool catalog (`ICommandTool` contributions): `create_project`, `stamp_file`, `import_folder`,
      `list_templates` from the Templates plugin, each with an argument schema derived from the template library
- [ ] `DevHubCommand` model + `ICommandInterpreter` interface
- [ ] `CommandValidator`: checks a command against the real library (template exists,
      vars valid) — reused by every interpreter
- [ ] Rule-based interpreter (keywords + fuzzy template-name match) as the baseline
- [ ] Command bar in the UI → confirmation screen → existing generate flow

### Stage 1 — ML.NET intent model (trained from scratch, C#)
- [ ] Synthetic dataset generator (phrasings × templates × values) → CSV/JSONL,
      fixed train/test split with a fixed random seed
- [ ] `DevHub.Training` console project: train intent classifier, print accuracy
      + confusion matrix, save model zip
- [ ] `MlNetCommandInterpreter`: ML.NET intent + rule/fuzzy slot filling
- [ ] Evaluation harness: run any `ICommandInterpreter` over the test set, report
      intent accuracy and exact-match on arguments
- [ ] Opt-in local log of real requests + corrections to grow the dataset

### Stage 2 — fine-tuned small open model (optional)
- [ ] Decide hardware/runtime (GPU? ONNX Runtime GenAI vs llama.cpp/Ollama)
- [ ] Python training folder (`training/llm/`): LoRA fine-tune a ~0.5–2B open
      model to emit the command JSON, using the same dataset
- [ ] Export/quantize; `LlmCommandInterpreter` with JSON-constrained output
- [ ] Compare against Stage 1 with the evaluation harness; write up the results

### Later (maybe)
- [ ] Expose the tool catalog as a real MCP server so external clients can drive DevHub
