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
- [ ] **NEXT — Integrated Git client (in-app, "Fork"-like).** Large; a full git UI
      (status/stage/commit/diff/branches/history/remotes) built inside the app.
      Plan it as its own milestone before building.

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
      (read-only), change count in the sidebar, dirty dot on the tab; Repositories tool window
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
- [ ] G8. Stashes, tags, cherry-pick, revert, blame, submodules

## M0 — Skeleton
- [x] `git init`, .NET `.gitignore`, `DevHub.sln`
- [x] `src/DevHub.Core` (net10.0 class library)
- [x] `src/DevHub.App` (Avalonia 12 app, Fluent theme, compiled bindings on by default)
- [x] `tests/DevHub.Core.Tests` (xUnit) referencing Core
- [x] DI container in `App.axaml.cs`, `ViewLocator`, `MainWindow` + `MainWindowViewModel`
- [x] `dotnet build` and `dotnet test` green; the app opens a dark app-shell window
      (sidebar nav: Templates / Generate / Import / Settings, placeholder pages)

## M1 — Template model & library (Core)
- [ ] Manifest model classes matching `docs/TEMPLATE_FORMAT.md`
- [ ] `TemplateLibrary`: resolve library root, list/load/save/delete templates
- [ ] Manifest validation (ids, variable names, choices, `when` refers to a bool)
- [ ] Unknown JSON fields round-trip; bad `schemaVersion` rejected
- [ ] Tests for all of the above

## M2 — Placeholder renderer (Core)
- [ ] Tokenizer: `{{Name}}`, `{{Name:transform}}`, `\{{` escape
- [ ] Transforms: pascal/camel/kebab/snake/upper/lower
- [ ] Built-ins: Year, Date, Author, Guid1..9 (stable per generation)
- [ ] Unknown tokens pass through verbatim (test with Vue/Jinja/GH Actions samples)
- [ ] Path segment rendering + validation

## M3 — Project generation (Core)
- [ ] Binary detection
- [ ] Generate into staging dir → conflict check → move into target
- [ ] Path-escape protection
- [ ] Encoding/BOM/line-ending preservation
- [ ] Tests using temp directories

## M4 — Main UI: browse + generate
- [ ] Template list (project/file tabs, filter by language/tag, search)
- [ ] Variable form generated from the manifest (string/bool/choice, validation messages)
- [ ] Target folder picker (via `IDialogService`)
- [ ] Generate button, progress + result/error display

## M5 — Template editor UI
- [ ] Create/edit/delete template metadata + variables
- [ ] Content tree view of the template's `content/` folder
- [ ] Add/rename/delete files and folders; simple text editor for file contents
- [ ] "Open in Explorer / VS Code" shortcuts

## M6 — File/script templates
- [ ] Stamp a file template into a chosen folder (conflict prompt)
- [ ] Namespace inference (from folder path + nearest `.csproj` RootNamespace)
- [ ] Starter file templates: C# class, interface, Unity MonoBehaviour, ScriptableObject, Python module

## M7 — Import from existing folder
- [ ] Ignore-glob matcher (Core, tested)
- [ ] Import preview: file tree with ignored items greyed out
- [ ] Literal → variable replacement with per-file hit counts
- [ ] Save as new project template

## M8 — Post-create commands
- [ ] Command runner (Core): ArgumentList, streamed stdout/stderr, cancellation, exit codes
- [ ] Windows `.cmd` shims (`npm`, `npx`, `yarn`) resolution
- [ ] Confirmation screen listing exact commands before running
- [ ] Live output log in the UI

## M9 — Polish
- [ ] Settings (author name, library location, default target folder)
- [ ] Recent generations list
- [ ] Zip export/import of a template
- [ ] App icon, publish profile (`dotnet publish` self-contained win-x64)

## M10 — Natural-language commands (local AI, learning project)
Goal: type "new unity project called Timber with git" → a structured tool call →
confirmation screen → same execution path as the UI. See "Natural-language
commands" in `CLAUDE.md` for the design rules.

### Stage 0 — plumbing (no ML yet)
- [ ] Tool catalog in Core: `create_project`, `stamp_file`, `import_folder`,
      `list_templates`, each with an argument schema derived from the template library
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
