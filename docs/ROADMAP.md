# DevHub Roadmap

Check boxes as tasks land. Each task should end in something that builds (and,
for Core, has tests). `/next-step` reads this file to decide what's next.

## Repository workbench (active track)
A parallel track turning DevHub into a git/project workbench (alongside the template
milestones below).
- [x] Repositories page: designate folders, scan for git repos, list them
- [x] Per-repo language detection (marker files; incl. Unity and Tauri)
- [x] "Open with the right tool" + per-language tool map persisted to `%APPDATA%/DevHub/settings.json`
- [ ] **NEXT — Integrated Git client (in-app, "Fork"-like).** Large; a full git UI
      (status/stage/commit/diff/branches/history/remotes) built inside the app.
      Plan it as its own milestone before building.

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
