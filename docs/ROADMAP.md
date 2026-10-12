# DevHub Roadmap

Check boxes as tasks land. Each task should end in something that builds (and,
for Core, has tests). `/next-step` reads this file to decide what's next.

## Repository workbench (active track)
A parallel track turning DevHub into a git/project workbench (alongside the template
milestones below).
- [x] Repositories page: designate folders, scan for git repos, list them
- [x] Per-repo language detection (marker files; incl. Unity and Tauri)
- [x] "Open with the right tool" + per-language tool map persisted to `%APPDATA%/DevHub/settings.json`
- [x] UI-0: JetBrains-style front end (see below) — design system + IDE shell,
      built before the Git client so the Git UI is mostly composition. Rules and recipes:
      `docs/UI_GUIDE.md`.
- [x] Integrated Git client (in-app, "Fork"-like): milestones G0–G9 below.

## UI-0 — JetBrains-style front end (Rider New UI look)
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

## G — Integrated Git client (Fork / JetBrains-style)
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

## P — Plugin architecture (active track, before M1)
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

## M0 — Skeleton
- [x] `git init`, .NET `.gitignore`, `DevHub.sln`
- [x] `src/DevHub.Core` (net10.0 class library)
- [x] `src/DevHub.App` (Avalonia 12 app, Fluent theme, compiled bindings on by default)
- [x] `tests/DevHub.Core.Tests` (xUnit) referencing Core
- [x] DI container in `App.axaml.cs`, `ViewLocator`, `MainWindow` + `MainWindowViewModel`
- [x] `dotnet build` and `dotnet test` green; the app opens a dark app-shell window
      (sidebar nav: Templates / Generate / Import / Settings, placeholder pages)

## M1 — Template model & libraries (Core)
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

## M2 — Placeholder renderer (Core)
- [x] Tokenizer: `{{Name}}`, `{{Name:transform}}`, `\{{` escape (one pass: values never re-scanned)
- [x] Transforms: pascal/camel/kebab/snake/upper/lower (word split on separators + case changes)
- [x] Built-ins: Year, Date, Author, Guid1..9 (stable per generation)
- [x] Unknown tokens pass through verbatim (Vue, Jinja, GH Actions, Handlebars, Helm samples)
- [x] Path segment rendering + validation (Windows names); commands (+ `when`), step paths, rootFolder;
      entered values checked (required / pattern / choice / bool) — 61 tests

## M3 — Generation pipeline (Core)
- [x] Command runner: ArgumentList, streamed output, cancellation, exit codes, `.cmd` shims (npm/npx), tool aliases (`@unity`)
- [x] Staging + base command (`runIn` root / parent); overlay of `content/` (binary detection, encoding/BOM kept)
- [x] Steps engine (createFolder / move / rename / delete, globs, nothing outside the root)
- [x] Result tree model for the preview; preview edits → applied + convertible to `steps` ("Save to template")
- [x] Conflict check, place into target; item templates applied into an existing folder (incl. moving existing files)
- [x] Post-create commands; the "commands that will run" list for the confirmation; tests in temp folders

## M4 — Browse + generate UI
- [x] Template browser: group by language or category, filter by both + library, search; project / item
- [x] Variable form from the manifest (string / bool / choice, validation messages); target folder picker; Raw
- [x] Confirmation (every command, rendered) → progress → editable preview tree (rename / move to / delete /
      new folder, Save to template) → place → post-create with live output
- [x] First built-ins to try it with: Console App (.NET, `dotnet new` + steps + post-create), Empty Project (folders only)

## M5 — Template editor UI
- [x] Templates tab (library manager: New Template…, Edit, Use in New Project, Duplicate, Delete) + one editor tab per template:
      General, Variables, Base Command, Steps, Post-create (Save Ctrl+S, Revert, asks before losing changes)
- [x] Files: tree of `content/` (new file / folder, rename, delete), files edited in your external editor (Open With)
- [x] template.json section (edit the raw manifest; read-only for built-ins); Duplicate to Personal; Explorer / VS Code
- [ ] In-app text editor for content files (chose external editors for now)

## M6 — Item templates
- [x] Add Files tab (Details → Preview → Done), opened from Projects (right-click), a repository tab, the Templates tab, File menu
      (hook `IAddFilesFromTemplate` in the Sdk); conflicts with an Overwrite choice each
- [x] Moves / deletes of existing files spelled out in the preview; only what changes shown (or everything)
- [x] Namespace inference (nearest `.csproj` RootNamespace or name, `.asmdef` rootNamespace, Unity project; + folders)
- [x] Starter items: C# class, interface, Unity MonoBehaviour, ScriptableObject, Python module
- [x] Templates tool window (left stripe, Alt+2): the list at hand; double-click / Use starts New Project or Add Files, Edit, New Template

## M7 — Import from an existing folder
- [x] Ignore rules, gitignore-style (name anywhere, name/ folders, /path from the top, globs) (Core, tested)
- [x] Import tab: preview tree with left-out items greyed out and placeholders per file
- [x] Literal → variable replacement; other spellings of a multi-word literal become transforms; an existing {{Var}} is escaped
- [x] Save as a new template (project or item) in a chosen library; it opens in the editor

## M8 — Built-in templates
- [x] .NET (console, classlib, solution with tests, add project to solution, Avalonia, xUnit), Unity (`@unity`, newest Hub editor)
- [x] Web (Vite React/Vue/Svelte TS, Tauri), Rust (cargo bin/lib), Python (src layout + .venv), Go (go mod)
- [x] Each checked against the real tool when installed (npm ones with DEVHUB_NETWORK_TESTS=1; Unity by its command)

## M9 — Polish
- [x] Settings → Templates (author name, personal library location, extra libraries, default project folder,
      project folder rules per language and/or category)
- [x] Recent projects on New Project's start page (Open / Show)
- [x] Preview: drag and drop to move files and folders (onto a folder, a file = its folder, empty space = the top), Undo (Ctrl+Z)
- [x] Zip export/import of a template (Templates tab; zip-slip refused, free id when taken)
- [x] App icon, publish profile (`dotnet publish src/DevHub.App -p:PublishProfile=win-x64` → artifacts/publish/win-x64)

## M10 — Natural-language commands (local AI, learning project)
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
