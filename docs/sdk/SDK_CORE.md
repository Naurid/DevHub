# DevHub.Sdk.Core reference

The part of the Sdk without UI: no Avalonia, no windows, everything unit-testable
(`tests/DevHub.Sdk.Core.Tests`). A plugin's `.Core` project references this package only. Overview and the other
package: [README.md](README.md), [SDK.md](SDK.md).

---

## Settings (`DevHub.Sdk.Core.Settings`)

DevHub keeps one settings file, `settings.json` under `%APPDATA%\DevHub\` (or under `DEVHUB_DATA_DIR` when that's
set, for experiments). The shell's own sections are typed properties of `AppSettings`; **each plugin keeps its
settings in a section of its own**, so the shell never needs a plugin's types.

### `ISettingsStore`

```csharp
AppSettings Load();             // defaults when nothing is saved (a missing or corrupt file never throws)
void Save(AppSettings settings);
```

Injected everywhere settings are read or written. The implementation is `JsonSettingsStore` (pretty-printed JSON;
the constructor takes a base folder so tests can use a temp folder).

### `ISettingsSection`: a plugin's settings

A plain class with public get/set properties and a parameterless constructor, plus a static `SectionId`, the
plugin's id. It's stored as `"plugins": { "<SectionId>": { … } }` in the file.

```csharp
public sealed class HelloSettings : ISettingsSection
{
    public static string SectionId => "naurid.hello";

    public bool Loud { get; set; }
    public List<string> Names { get; set; } = new();
}
```

### `SettingsStoreExtensions`

| Method | Use |
|---|---|
| `store.LoadSection<T>()` | A plugin section, freshly loaded (defaults when never saved or unreadable). |
| `store.UpdateSection<T>(s => s.Loud = true)` | Load → change the section → save. |
| `store.Update(s => s.Language = "fr")` | The same for the shell's own sections. |

**Always change settings with `Update` / `UpdateSection`**, never by keeping an `AppSettings` (or section) object
and saving it later: several features write the same file, and saving an old copy would overwrite what the others
saved in the meantime (a "lost update").

### `AppSettings`

The whole file. Plugins rarely need it directly. Shell fields: `ToolWindows` (`ToolWindowLayout`), `Language`,
`Appearance` (`AppearanceSettings`), `Updates` (`UpdateSettings`), `Editor` (`EditorLayout`, the open tabs),
`EnabledPlugins` (null = first start: everything loads and the first-run picker opens), `PluginSources`
(marketplace indexes), `Plugins` (the sections, as raw JSON) and `Extra` (fields written by a newer DevHub, kept on
save). `GetSection<T>()` / `SetSection<T>()` read and write one section.

Sections of plugins that are switched off are kept as they are, so switching a plugin back on finds its settings.

---

## Plugins (`DevHub.Sdk.Core.Plugins`)

### `PluginInfo`

What a plugin is, as plain data (the host reads it before loading anything else):

```csharp
public sealed record PluginInfo(string Id, string Name, string Description, IReadOnlyList<string> Requires)
{
    public string Category { get; init; } = "General";   // grouping on the Plugins page
    public string Version { get; init; } = "1.0";
    public int Order { get; init; } = 1000;              // load order among unrelated plugins, menu group order
    public IReadOnlyList<string> Presets { get; init; } = []; // first-run presets it belongs to
}
```

- `Id`: lowercase and dotted (`devhub.git`, `yourname.plugin-name`). Also the key of its settings section and the
  id in `plugin.json`; the two must match.
- `Requires`: ids of plugins it can't work without. They're switched on with it and load before it.
- `Presets`: names like `"Git client"` or `"Planning"`. The first-run picker offers every preset any plugin names,
  plus "Everything"; choosing one switches on exactly its plugins (and their requirements).

### `PluginResolver`

`Resolve(available, enabled)` decides what loads: the switched-on plugins plus their requirements, requirements
first. It returns a `PluginResolution` with `Loaded` and `Skipped` (each `SkippedPlugin` has a reason: a missing
requirement, a circle of requirements, a duplicate id). Also `RequirementsFirst(…)` (sort anything by
requirements) and `Dependents(plugins, id)` (what stops working when a plugin is switched off).

### `PluginSelection`

The user's choice while they click in Settings → Plugins or the first-run picker. `Enable(id)` also switches on
what it requires and `Disable(id)` also switches off what requires it; both return the other plugins they touched,
so the UI can say so. `Presets`, `ApplyPreset(name)`, `MatchingPreset`, and `Everything` (the preset that switches
everything on).

---

## Marketplace (`DevHub.Sdk.Core.Plugins.Marketplace`)

Used by the shell; plugin authors mostly need `PluginApi` and the `plugin.json` format.

| Type | What it is |
|---|---|
| `PluginApi` | `Version` (the plugin API version, currently 1) and `DefaultIndexUrl` (DevHub-Plugins' `index.json`). |
| `PluginPackageInfo` | `plugin.json`: `id`, `name`, `version`, `description`, `category`, `apiVersion`, `requires`. |
| `IndexEntry` | One plugin in a marketplace index (adds the download URL, SHA-256, size); `IsCompatible` checks the API version. |
| `Marketplace` | `ParseIndex(json)`, `LoadAsync(http, sources)` (the first source wins for an id; a failing source is reported, not fatal), `IsNewer(a, b)` (numeric version compare). |
| `PluginInstaller` | Installs and removes plugins under `%AppData%\DevHub\plugins`. Changes go to `.pending` and `ApplyPending` moves them into place at the next start (a running DevHub has the DLLs open). Downloads are checked against their SHA-256; a zip entry outside the plugin folder stops the install. |
| `InstallPlanner` | `PlanInstall` (what else must be installed, requirements first, or why it can't be) and `Dependents` (what a removal takes with it). |
| `PluginFolders` | Where plugin folders are: bundled (`plugins\` next to the exe), installed, and development folders from `DEVHUB_PLUGIN_PATHS`. `Choose` picks one folder per id (the newer of bundled and installed; a development folder always wins). |

---

## Localization (`DevHub.Sdk.Core.Localization`)

### `L`

DevHub speaks English and French. **The English text is the key**:

```csharp
L.T("Apply")                                   // "Appliquer" in French
L.T($"{count} issues moved to {name}")         // looked up as "{0} issues moved to {1}", then filled in
L.F("{0} issues", count)                       // the same with an explicit format
```

- Text without a translation stays English, so a plugin without a French file still works.
- Translations are an embedded resource ending in `i18n.fr.json` (a JSON object, English → French) in any loaded
  assembly: `<EmbeddedResource Include="i18n\fr.json" />`.
- Each assembly's own file wins for its own text ("Low" can be "Basse" in one plugin and "Faible" in another). Text
  an assembly doesn't translate itself is looked up in all the files together.
- The language is chosen once at startup (`L.Use`); a change in Settings applies after a restart. `L.Language`,
  `L.IsFrench`, `L.Languages`, `L.Resolve` are there for the few places that format by language.
- In XAML use `{l:T 'Apply'}` from `DevHub.Sdk` ([SDK.md](SDK.md#localization-devhublocalization)).
- Translate `AppAction.Text`, titles and messages. Don't translate ids (action ids, tool window ids, page ids):
  they're saved in settings.

---

## Search (`DevHub.Sdk.Core.Search`)

### `FuzzyMatcher`

Find-Action-style matching: every pattern character must appear in the text in order, case-insensitive ("gps"
matches "Git: Push"). Word starts and runs of consecutive characters score extra, skipped characters cost a little.

```csharp
FuzzyMatch? m = FuzzyMatcher.Match("gp", "Git: Push");   // null when it doesn't match
// m.Score (higher is better), m.Positions (indexes to highlight)
```

An empty pattern matches everything with score 0; spaces in the pattern are ignored. Details:
[../ACTIONS_AND_FUZZY_MATCHING.md](../ACTIONS_AND_FUZZY_MATCHING.md).

---

## Formatting (`DevHub.Sdk.Core.Formatting`)

| Type | What it does |
|---|---|
| `Markdown` | `Parse(text)` → blocks (`MarkdownHeading`, `MarkdownParagraph`, `MarkdownCodeBlock`, `MarkdownQuote`, `MarkdownList` of `MarkdownListItem`, `MarkdownRule`) made of styled `MarkdownRun`s. A common subset of CommonMark and GitHub's flavor: headings, paragraphs, code, quotes, nested and task lists, rules, bold, italic, code, strikethrough, links and autolinks. No HTML (shown as text). Never throws. `IsSafeLink(url)`: only http(s) and mailto. The UI side is `MarkdownView` in the Sdk. |
| `PathTree` | `Build(items, path, compress)` turns `/`-separated paths into a tree of `PathTreeNode<T>` (folders first, by name). `compress` merges single-child folder chains ("src/DevHub.App"). `Flatten` visits depth-first. Used for branch folders and changed-file trees. |
| `RelativeTime` | `Format(when, now)`: "just now", "5 minutes ago", "yesterday", "3 days ago", then a date ("Oct 7"). Translated. |

---

## Charts (`DevHub.Sdk.Core.Charts`)

The data side of the Sdk's charts. A plugin's `.Core` builds a `ChartData` (a burndown from issue history, run
durations…); a view shows it with a chart control from `DevHub.Controls` ([SDK.md](SDK.md#charts)).

```csharp
var velocity = new ChartData(
    ["Sprint 1", "Sprint 2", "Sprint 3"],
    [new ChartSeries("Committed", [21, 18, 24]), new ChartSeries("Done", [17, 18, 22])])
{
    Title = "Velocity", XTitle = "Sprint", YTitle = "Story points", ValueSuffix = " pts",
};
```

| Type | What it is |
|---|---|
| `ChartData` | `Labels` (X axis, pie slices, heatmap columns) and `Series`. Optional: `Title`, `XTitle`, `YTitle`, `ValueSuffix`, `Bands` (shaded ranges), `References` (horizontal lines: a target, a capacity), `X` (a scatter chart's numbers), `CellText` (heatmap cell text), `Max` (a gauge's scale), `TrafficColors` (a green → amber → red heatmap). `Of(name, points)`, `Range()`, `StackedRange()`, `ToTable()`, `Format(value)`. |
| `ChartSeries` | A name and one `double?` per label (null = a gap), plus a `SeriesStyle`. |
| `SeriesStyle` | `Dash` (`LineDash`), `Markers`, `ValueLabels`, `Color` (palette index 0–5 = `Brush.Chart.1…6`), `Kind` (`SeriesKind`: bars or a line in a combo chart), `Curved`. |
| `ChartBand`, `ChartReference` | A shaded low–high range per label; a horizontal line at a value. |
| `ChartKind` | Line, Area, StackedArea, Bar, StackedBar, HorizontalBar, StackedHorizontalBar, Combo, Scatter, Pie, Donut, Heatmap, Gauge. |
| `ChartKinds` | `Name(kind)` (translated) and `Fitting(data)`: the kinds that can show this data, for a chart type picker. |
| `ChartMath` | The math the controls share, tested: `NiceScale` (readable axes), `SmoothSegments` (curves that never overshoot), `Pie`, `Histogram`, `Short` ("1.2k"). |

Charts and tables go out of DevHub (PNG, clipboard, CSV) through `IExportService` in the Sdk.

---

## Tables (`DevHub.Sdk.Core.Tables`)

`TableData(Headers, Rows)` with an optional `Title`: a table to export. `ToCsv()` (RFC 4180, opens in Excel and
Sheets) for files, `ToTsv()` for the clipboard (pastes as cells in Word, Excel, Sheets). `ChartData.ToTable()`
makes one from a chart.

---

## People (`DevHub.Sdk.Core.People`)

`Person(Id, Name, Email?, AvatarUrl?)`: someone a plugin can assign or mention. `Id` is whatever is stable for the
plugin (a GitHub login, an email); `Initials` is what an avatar shows without a picture.
`PeopleSearch.Find(people, query, exclude, limit)` finds people fuzzily by name, id or email. The UI side is
`PeoplePicker` in the Sdk.

---

## Collections (`DevHub.Sdk.Core.Collections`)

`ListMoves.Move(from, item, to, toIndex)` moves an item within a list or between lists, the way drag and drop means
it: `toIndex` counts positions in the target list **before** the move, which is exactly what the Sdk's
`ItemsDragDrop` reports. `IsNoOp(…)` tells "dropped right where it was" apart.
