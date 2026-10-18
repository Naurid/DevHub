# DevHub.Sdk reference

What every plugin's UI builds on: the plugin contract, the ViewModel base classes the shell turns into tabs, tool
windows, dialogs and settings pages, actions, the shell's services, the hooks between plugins, controls, charts and
the design system. Built on Avalonia 12 and CommunityToolkit.Mvvm. Overview: [README.md](README.md); the UI-free
half: [SDK_CORE.md](SDK_CORE.md). How the pieces fit in a real plugin: [../PLUGINS.md](../PLUGINS.md) and the
recipes in [../UI_GUIDE.md §5](../UI_GUIDE.md).

Diagrams (open them in DevHub with the Diagrams plugins): `docs/diagrams/Plugin contract`, `Shell and Sdk` and
`Startup and plugin loading`.

---

## The plugin contract (`DevHub.Plugins`)

### `IPlugin`

```csharp
public interface IPlugin
{
    PluginInfo Info { get; }
    void ConfigureServices(IServiceCollection services, PluginContext context);
    IEnumerable<Uri> Resources => [];
}
```

- **`Info`**: the plugin's `PluginInfo` ([SDK_CORE.md](SDK_CORE.md#plugininfo)); its id equals `plugin.json`'s.
- **`ConfigureServices`** runs once at startup, before the DI container is built, and only when the plugin is
  switched on and its requirements loaded. Everything the plugin adds is registered here (next section). A plugin
  that's off registers nothing, so none of its menus, tabs or shortcuts exist.
- **`Resources`**: resource dictionaries merged into the app's resources before any window opens, typically the
  plugin's own design tokens: `new Uri("avares://DevHub.Git/Themes/GitTokens.axaml")`.

### `PluginContext`

`PluginContext(ISettingsStore Settings, IReadOnlySet<string> LoadedPlugins)`: what the host hands over during
registration. `Settings` reads the plugin's section if registration depends on it; `LoadedPlugins` holds the ids of
every plugin loading this run (for an optional integration that must know).

### `[assembly: DevHubPlugin(typeof(MyPlugin))]`

Marks the assembly; the type needs a public parameterless constructor. One assembly may hold several plugins.

### Where a plugin contributes

Everything goes through DI. The shell collects each kind of contribution:

| Register | As | Becomes |
|---|---|---|
| `services.AddSingleton<IActionProvider, MyActions>()` | `IActionProvider` | actions: menus, toolbar, Welcome, shortcuts, Find Action |
| `services.AddSingleton<DocumentViewModelBase, MyDocument>()` | `DocumentViewModelBase` | a single editor tab, with an `Navigate.<Id>` action |
| `services.AddSingleton<IDocumentFactory, MyFactory>()` | `IDocumentFactory` | tabs opened many times, one per id (`Repository:C:\…`) |
| `services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<MyToolWindow>())` | `ToolWindowViewModelBase` | a docked panel with a stripe icon and Alt+N |
| `services.AddTransient<SettingsPageViewModelBase, MySettingsPage>()` | `SettingsPageViewModelBase` (**transient**) | a page of the Settings dialog |
| `services.AddSingleton<TitleBarWidgetViewModelBase, MyWidget>()` | `TitleBarWidgetViewModelBase` | a widget in the title bar |
| `services.AddSingleton<StatusBarWidgetViewModelBase, MyWidget>()` | `StatusBarWidgetViewModelBase` | a widget in the status bar |
| a hook from `DevHub.Integration` | the hook interface | an optional link to other plugins ([below](#hooks-between-plugins-devhubintegration)) |
| its own services | anything | for itself, and for plugins that require it |

Views are found by name: `Foo.ViewModels.BarViewModel` → `Foo.Views.BarView`, in the ViewModel's assembly. Debug
builds validate every registration at startup (`ValidateOnBuild`), so a missing dependency fails immediately.

---

## ViewModels (`DevHub.ViewModels`)

All ViewModels derive from **`ViewModelBase`** (CommunityToolkit's `ObservableObject`: `[ObservableProperty]` and
`[RelayCommand]` source generators raise the change notifications compiled bindings listen to). ViewModels never
reference views, windows or Avalonia controls; they talk to the shell through the services below.

### Documents: editor tabs (`DevHub.ViewModels.Documents`)

**`DocumentViewModelBase`**

| Member | Meaning |
|---|---|
| `Id` | Stable id saved to reopen tabs (default: the class name without "ViewModel"). |
| `Title`, `Icon` (abstract) | Tab label and an `IconKind` name. |
| `Navigation` | `DocumentNavigation`: how its `Navigate.<Id>` action looks (`Text`, `Gesture`, `Menu`, `MenuOrder`, `WelcomeOrder`). Single documents only. |
| `FillsEditorArea` | True: the view lays out its own edges (a repository tab); false: the editor area pads it. |
| `ContextPath` | The folder the tab is about (a repository), so other plugins can act on "the tab in front". |
| `IsDirty` | Shows a dot on the tab. |
| `HasUnsavedChanges` | Edits lost when the tab closes; closing the window asks first. |
| `ConfirmCloseAsync()` | Asked before the tab closes; return false to keep it open. |
| `OnActivationChanged(bool)` | The tab came to the front or left it (e.g. refresh). |
| `IsActive`, `CloseCommand`, `OpenDocument` | Set by the shell. `OpenDocument(id)` opens another tab by id. |

A single document is a DI singleton, so closing and reopening its tab keeps its state.

**`IDocumentFactory`**: `IdPrefix` ("Repository:", with the separator) and `Create(id)` (null when it can't be made
any more, e.g. the folder is gone). The shell asks the factory whose prefix matches, both when opening and when
restoring tabs at startup. The created document's `Id` must equal the id it was asked for.

**`IDocumentService`**: the editor area as plugins see it. `Active` (get / set), `OpenDocuments`,
`OpenDocumentsChanged`, `OpenById(id)`; raises `PropertyChanged` for `Active`.

### Tool windows (`DevHub.ViewModels.ToolWindows`)

**`ToolWindowViewModelBase`**: `Id` (saved in settings: never translate or rename it), `Title`, `IconKind`, `Slot`
(`ToolWindowSlot.Left`, `Right`, `Bottom`), `Order` (position in the stripes, the View menu and Alt+1…9; Projects is
0), `WelcomeOrder`. `IsOpen`, `ToggleCommand` and `Shortcut` are set by the shell. The view usually wraps itself in a
`ToolWindowFrame`.

### Dialogs (`DevHub.ViewModels.Dialogs`)

**`DialogViewModelBase`**, shown with `IDialogService.ShowDialogAsync(dialog)` (true = OK). The dialog window
supplies the frame and the footer; the subclass's view is the content.

| Member | Meaning |
|---|---|
| `Title` (abstract) | Window title. |
| `Width`, `Height`, `MinWidth`, `MinHeight`, `FitsContent` | Size; `FitsContent` makes it as tall as its content. |
| `OkText` | The OK button says what it does ("Discard"). |
| `IsDestructive` | Red OK button. |
| `ShowCancel`, `ShowApply` | Footer buttons. |
| `CanApply()`, `ApplyChangesAsync()` | Override to validate and save; return false to stay open. |
| `CloseRequested` | Raised with the result; the service closes the window. |

**`ConfirmDialogViewModel(title, message, okText, items, destructive, showCancel)`**: the ready-made "are you
sure?" dialog. Lists up to 8 affected items, then "and N more". With `showCancel: false` it's a message to
acknowledge.

### Settings pages (`DevHub.ViewModels.Dialogs.Settings`)

**`SettingsPageViewModelBase`**: `Title`, `Keywords` (extra words the Settings search matches), `Order` (Appearance is
0, Plugins 900; default 500). The page edits its own copy of the values, calls `MarkModified()` when the user changes
something, and saves in `Apply()`. Register as **transient**, so every opening of Settings starts from the saved
values.

### Widgets (`DevHub.ViewModels.Shell`)

**`TitleBarWidgetViewModelBase`** (next to the breadcrumb, e.g. Git's branch widget) and
**`StatusBarWidgetViewModelBase`** (left of the theme and density widgets). Both have `Order`. Interactive parts of a
title-bar widget need `WindowDecorationProperties.ElementRole="User"` so clicks don't drag the window.

### `AvatarViewModel`

`Image` (PNG / JPEG bytes, or null): one instance can be shared by every place showing the same person, so they all
update when the download finishes. Shown by the `Avatar` control.

---

## Actions (`DevHub.Actions`)

Everything the user can do is an `AppAction`, defined once and shown in menus, the toolbar, the Welcome view,
context menus, Find Action and through its shortcut. In depth: [../ACTIONS_AND_FUZZY_MATCHING.md](../ACTIONS_AND_FUZZY_MATCHING.md).

```csharp
public sealed class GitActions(GitCommands git) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction("Git.Commit", L.T("Commit…"), git.CommitCommand)
        {
            Category = "Git",          // English; Find Action shows it translated
            IconKind = IconKind.Commit,
            Gesture = "Ctrl+K",
            Menu = "Git", MenuOrder = 0,
        };
    }
}
```

| Member | Meaning |
|---|---|
| `Id` | Stable, `Area.Verb` ("Git.Commit"). Used by menus, keymaps and settings; never translated. |
| `Text` | The label ("Commit…": "…" when it opens a dialog). Translated with `L.T`. |
| `Command` | An `ICommand`; its `CanExecute` enables and disables every place the action shows. |
| `Category`, `CategoryText` | Find Action's group, in English and translated. |
| `IconKind`, `Description` | Icon and a longer tooltip text. |
| `Gesture` | Avalonia key gesture syntax ("Ctrl+Shift+K", "F5", "Alt+1"). |
| `Menu`, `MenuOrder` | Main-menu group and position. |
| `ToolbarOrder`, `WelcomeOrder` | Position on the title-bar toolbar / in the Welcome view; null = not there. |

Orders sort lowest first; a new hundred (0–99, 100–199…) starts a group with a separator in front.

| Type | Use |
|---|---|
| `IActionProvider` | `GetActions()`; register as a singleton. |
| `ActionRegistry` | Every action by id: `All`, `Get(id)` (throws on a typo), `TryGet(id, out action)`. A duplicate id fails at startup. |
| `ActionLayout.Arrange(actions, order)` | Sorts by an order property and inserts the group separators (null). |
| `MenuBuilder.Build(registry, ids)` | Menu items from ids, `"-"` for separators (edge and double separators dropped; ids of plugins that aren't loaded skipped). |
| `Gestures.Parse(text)` | Use instead of `KeyGesture.Parse` (Avalonia 12 reads "Alt+1" as the wrong key). |

**In XAML** (`xmlns:a="using:DevHub.Actions"`):

| Markup | Gives |
|---|---|
| `Action="{a:Action Git.Commit}"` | The action (on an `ActionButton`); the control hides when the action's plugin isn't loaded. |
| `ItemsSource="{a:ActionMenu 'Git.Commit,-,Git.Push'}"` | Menu items for a context menu. |
| `ItemsSource="{a:Actions 'Git.Clone,Git.OpenRepository'}"` | A list of actions (links, buttons). |
| `Command="{a:ActionCommand View.ToggleTheme}"` | Just the command, for a button with its own content. |
| `ItemsSource="{a:ActionList}"` | Every action (a shortcut list). |

`AppServices.Current` is the DI container the markup extensions read (null in the XAML previewer). Everything else
gets its services through its constructor.

---

## Services (`DevHub.Services`)

The seams between ViewModels and Avalonia. Inject them; never reach for `Window`, `TopLevel` or `Application`.

**`IDialogService`**

| Method | Does |
|---|---|
| `ShowDialogAsync(dialog)` | Modal dialog; true for OK. |
| `PickFolderAsync(title)` | Folder picker; null when cancelled. |
| `PickFileAsync(title, filterName, patterns)` | File picker (`["*.exe"]`). |
| `PickSaveFileAsync(title, suggestedName, filterName, extension)` | "Save as" picker. |

**`IStatusService`**: the status bar. `Show(message)` replaces the message on the left (null clears it).
`StartTask(title, cancel?)` shows a running operation until the returned `IStatusTask` is disposed; `Report(step,
percent)` updates it from any thread (null percent = indeterminate). Call `StartTask` from the UI thread.

```csharp
using var task = status.StartTask(L.T($"Pushing {branch}…"), cancel: () => cts.Cancel());
await git.PushAsync(progress => task.Report(progress.Step, progress.Percent), cts.Token);
status.Show(L.T($"Pushed {branch}."));
```

**`IThemeService`**: `Mode` (`ThemeMode.Dark`, `Light`, `System`), `IsDark`, `IsCompact`, `Accent` ("#RRGGBB"),
`SetAccent(hex)`, `Changed`. Every change is saved. Most UI follows by itself through `DynamicResource`; `Changed` is
for what can't (bitmaps) and for ViewModels that mirror the state.

**`IExportService`**: getting things out of DevHub for reports. Images are light by default (they print well); every
method reports in the status bar.

| Method | Does |
|---|---|
| `SaveChartAsync(kind, data, name)`, `CopyChartAsync(kind, data)` | A chart as PNG, to a file or the clipboard. |
| `SaveTableAsync(table, name)`, `CopyTableAsync(table)` | A `TableData` as CSV, or as cells (tab-separated) on the clipboard. |
| `SaveImageAsync(png, name)`, `CopyImageAsync(png)` | Any PNG (a diagram, a mood board). |
| `SaveTextAsync(text, name, extension, typeName)` | Text as UTF-8 (SVG, PlantUML, SQL). |
| `CopyTextAsync(text)` | Text to the clipboard. |
| `GetClipboardImageAsync()`, `GetClipboardFilesAsync()`, `GetClipboardTextAsync()` | Read the clipboard (pasted pictures, files from Explorer, text). |

---

## Hooks between plugins (`DevHub.Integration`)

Optional links between plugins that don't depend on each other. One side registers the interface; the other
injects it **optionally** (`T? hook = null`, or `IEnumerable<T>` and take the first) and has a fallback when it's
missing. Only plain data crosses (paths, SHAs, keys, records defined here), never a plugin's own types. See
[../PLUGINS.md §6](../PLUGINS.md) and the diagram `Hooks between plugins` in DevHub-Plugins.

| Hook | Offered by | Used by | Without it |
|---|---|---|---|
| `IProjectOpener.Open(folder)` | Git (a repository tab) | Projects (double-click, Open in DevHub), Templates (after New Project) | the folder opens in the project's external tool |
| `IProjectFileEditor.OpenFile(projectFolder, filePath, line)` | Projects (Settings → Open With) | Git (Open in Editor, F4), Templates | Windows' associated program |
| `IProjectFolders` (`Folders`, `RescanAsync()`) | Projects | Git (Clone's default folder, rescan), Templates | Clone asks for a folder |
| `IAddFilesFromTemplate.Open(folder)` | Templates | Projects (right-click), Git (repository tab) | the entry isn't shown |
| `IProjectCatalog.Projects` (`ProjectInfo(Name, Folder)`) | Projects | Diagrams (Link to Project…) | pick the project's folder |
| `IProjectDiagrams` (`DiagramsOf`, `Mentioning`, `Open`, `CreateAsync`, `Changed`) | Diagrams | Projects (Diagrams under each project), Board (Diagrams page, issue section) | nothing shown |
| `IIssueLinks` (`SearchAsync`, `FindAsync`, `Open`, `CreateStoryAsync`, `Changed`) | Board | Diagrams (shapes linked to issues) | the key stays plain text |
| `IChartMaker.Open(kind, data)` | Charts | Board Reports (Open in Chart Maker) | the button isn't shown |
| `IGitHubApi` (`GetAccountsAsync`, `SendAsync`) | GitHub | Board (GitHub Issues import), Board Git (an issue's pull requests) | no GitHub features |
| `IRepositoryEvents` (`BranchChanged`, `Committed`, `Pushed`, `Fetched`) | Git | Board Git (smart commits, issue moves) | nothing reacts |
| `ICommitMessageSuggester.SuggestAsync(context, ct)` | Board Git ("DEV-12: ") | Git's commit box | no suggestion |
| `IGitDecorator` (`DecorateCommits`, `DecorateBranches`, `Changed`) | Board Git (issue keys, the board branch) | Git's commit list, details, branch tree | no marks |

Rules that come with them:

- **`IGitHubApi`** sends requests only to api.github.com, as the account signed in through Git Credential Manager;
  the token never leaves the GitHub plugin. `GitHubApiRequest(Method, Path, Body, Account, IfNoneMatch, Accept)` and
  `GitHubApiResponse(Status, Body, ETag, NextPageUrl, RateLimitRemaining, RateLimitReset)`. Pace yourself with ETags
  (`IfNoneMatch`: a 304 doesn't count against the rate limit). No account or no network throws
  `GitHubApiUnavailableException`; an HTTP error is a response (`IsSuccess`).
- **`IRepositoryEvents`** are raised on the UI thread after the change. Unsubscribe when you're done.
- **`ICommitMessageSuggester`** must answer fast and without the network (or null). Git only offers the suggestion;
  it's never put in without the user seeing it.
- **`IGitDecorator`** answers from what it already knows: fetch in the background and raise `Changed`. A
  `Decoration(IconKind, Tone, ToolTip, Url)` has a `DecorationTone` (Neutral, Running, Success, Warning, Error) the
  view maps to `Brush.Status.*`; `Url` must be http(s).
- **`IProjectDiagrams.DiagramsOf` / `Mentioning`** read files: call them off the UI thread.

A plugin's own extension points for plugins that require it (Git's `IRemoteHost`, Board's page providers, Diagrams'
`IDiagramKind`) live in that plugin, not here: see `docs/plugins/` in DevHub-Plugins.

---

## Controls (`DevHub.Controls`)

`xmlns:c="using:DevHub.Controls"`. Use these before building new ones; their looks are in `Themes/Components/`.
Examples and rules: [../UI_GUIDE.md §4](../UI_GUIDE.md).

### Building blocks

| Control | What it is |
|---|---|
| `Icon` | A JetBrains expui icon: `<c:Icon Kind="folder" />`. Swaps to `{Kind}_dark.svg` in the dark theme, fades when disabled, white on accent buttons. A plugin's own icon: `Kind="avares://DevHub.Git/Assets/Icons/branch"` (no extension). |
| `IconKind` | The icon names as constants (`IconKind.Commit`, `IconKind.Refresh`…), plain strings ViewModels can use. Adding one: copy both SVGs from intellij-community's `platform/icons/src/expui/`, add a constant, list it in `Assets/Icons/NOTICE.md`. |
| `ActionButton` | Square icon-only button: `<c:ActionButton Action="{a:Action Projects.Rescan}" />` takes command, icon and tooltip ("Rescan (F5)") from the action. |
| `Toolbar`, `ToolbarSeparator` | A row of action buttons spaced by `Spacing.Toolbar`. |
| `ToolWindowFrame` | A tool window's chrome: `Header`, `HeaderActions` (usually a `Toolbar`), content. |
| `EmptyState` | Centered placeholder: `IconKind`, `Title`, `Description`, actions as content. |
| `SettingsRow` | One row of a settings page: `Label`, `Description`, the editor as content; rows line up. |
| `Badge` | Pill for counts and tags; classes `accent`, `success`, `warning`, `error`. |
| `SearchField` | A `TextBox` with a search icon and a clear button; Escape clears it. |
| `Avatar` | Round picture from `ImageData` (bytes), or the first letter of `Text` on the accent color. |
| `MarkdownView` | Read-only Markdown (`Markdown="{Binding Description}"`): the subset `Sdk.Core`'s `Markdown` parses; only http(s) and mailto links open. |
| `PeoplePicker` | Picks `Person`s from `People` into `SelectedPeople` (an `ObservableCollection`): fuzzy search, chips, keyboard. `AllowMultiple`, `Placeholder`. |

### Layout

| Control | What it is |
|---|---|
| `SidePane` | A side panel that folds into a rail (and opens over the content) when the content would get narrower than `MinContentWidth`. `Pane`, `Content`, `PaneWidth` (two-way, save it), `MinPaneWidth`, `MaxPaneFraction`, `IsRight`, `PaneTitle`, `PaneIcon`, `CloseOnSelection`, `CloseOnContentClick`, `IsCompact`. |
| `OverflowPanel` | One row that never wraps or clips: what doesn't fit goes into a « More » menu. As a tab strip's items panel or around buttons. `Spacing`, `MoreContent`, `HasOverflow`. |
| `SplitterThumb` | Drag handle that changes a bound size (`Value`, `Orientation`, `Invert`, `Minimum`, `Maximum`, `MaximumFraction`). |

### Behaviors (attached properties)

| Property | Does |
|---|---|
| `c:DoubleTap.Command` | Runs a command on double-click (rows that open something). |
| `c:FocusOnLoad.IsEnabled` | Focuses the element when it appears (a popup's search box). |
| `c:FocusRequest.Token` | Focuses the element each time a bound number changes (a ViewModel asking for focus). |
| `c:ItemsDragDrop.Handler` (+ `List`, `Orientation`) | Drag and drop between items of lists, decided by an `IItemsDropHandler` (`CanDrop`, `Drop` with an `ItemsDrop`); pass its index to `ListMoves.Move`. Nested lists stay apart. |
| `c:TreeDragDrop.Handler` | Drag and drop between rows of a `TreeView`, decided by an `ITreeDropHandler`. |
| `ClickAwayDeselect` | Installed by the shell: clicking empty space in any list or tree clears its selection. Opt out with `SelectionMode="Single,AlwaysSelected"`. |

### Charts

Drawn by the Sdk (no chart library), colored from `Brush.Chart.1…6`, with a legend, tooltips and an empty text. All
take `Data` (a `ChartData`, see [SDK_CORE.md](SDK_CORE.md#charts-devhubsdkcorecharts)), `EmptyText` and `ShowTitle`.

| Control | Shows |
|---|---|
| `Chart` | Any `ChartKind`: `<c:Chart Kind="{Binding Kind}" Data="{Binding Data}" />`, for a "chart type" picker. |
| `LineChart` (`IsZeroBased`, `IsArea`, `IsStacked`), `AreaChart` | Lines, areas, stacked areas. |
| `BarChart` (`IsStacked`), `HorizontalBarChart`, `ComboChart` | Bars side by side or stacked; horizontal; bars and lines together. |
| `ScatterChart`, `HeatmapChart`, `GaugeChart`, `PieChart` | Points, a grid of cells, a single value, slices (pie or donut). |
| `ChartImage` | A chart drawn off screen as PNG (what `IExportService` saves). |

---

## Converters (`DevHub.Converters`)

| Converter | Does |
|---|---|
| `FractionConverter` | A number × `ConverterParameter` ("at most 45% of the window"). |
| `HexToBrushConverter` | "#RRGGBB" → a brush (ViewModels keep colors as strings). |
| `IndentConverter` (`Step`) | A tree depth → a left margin. |

---

## Localization (`DevHub.Localization`)

`Text="{l:T 'Edit Selected…'}"` with `xmlns:l="using:DevHub.Localization"`: the XAML side of `L.T`
([SDK_CORE.md](SDK_CORE.md#localization-devhubsdkcorelocalization)). The view's own assembly is asked first, like
`L.T` in C#. The value is read once (the language is fixed at startup).

---

## The design system (`Themes/`)

The tokens every view uses through `DynamicResource`, never hex colors, pixel sizes or font names. Full list and
rules: [../UI_GUIDE.md §3](../UI_GUIDE.md).

| File | Holds |
|---|---|
| `Tokens/Colors.axaml` | `Brush.*` for Dark and Light (backgrounds, text, borders, accent, status, chart palette). |
| `Tokens/Sizes.axaml`, `Tokens/Sizes.Compact.axaml` | `Size.*`, `Spacing.*`, `Padding.*`, `Radius.*`; Compact overrides. |
| `Tokens/Fonts.axaml` | Inter for UI, JetBrains Mono for code. |
| `Controls.axaml`, `FluentMapping.axaml`, `Components/*.axaml` | Styles for standard controls, Fluent's keys mapped to the tokens, the Sdk components' looks. |
| `DevHubTheme.axaml` | Includes all of it (the shell loads it). |

Text classes: `header`, `secondary`, `small`, `code`. A plugin's own tokens go in its `Themes/*.axaml`, returned from
`IPlugin.Resources`, and follow the same Dark / Light pattern.
