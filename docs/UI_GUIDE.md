# DevHub UI guide

How the DevHub window is built (the JetBrains "New UI" look from roadmap milestone UI-0),
the rules every view follows, and step-by-step recipes for adding things. Read this before
changing anything under `src/DevHub.App`, `src/DevHub.Sdk` or a plugin's UI project (`plugins/DevHub.*`).

Related: [`ACTIONS_AND_FUZZY_MATCHING.md`](ACTIONS_AND_FUZZY_MATCHING.md) explains actions
and Find Action in depth. The UI Gallery (Debug builds: ☰ → View → UI Gallery) shows every
token and component live; check changes there in Dark, Light and Compact.

---

## 1. The rules

1. **No hex colors, pixel sizes or font names in views.** Use the tokens (§3):
   `{DynamicResource Brush.Text.Secondary}`, `{DynamicResource Spacing}`,
   `<TextBlock Classes="header">`. Only the token files (`Themes/Tokens/`) contain raw values.
   *Why:* Dark/Light, the accent color and Compact mode all work by swapping token values.
   A hard-coded value never changes.
2. **Always `DynamicResource` for tokens, never `StaticResource`.** Static is resolved
   once, so it would miss theme, accent and Compact changes.
3. **Match the token's type to the property.** Padding/Margin need a `Padding.*` /
   `Margin.*` (Thickness); `Spacing`/`Width`/`Height` need `Size.*` / `Spacing.*` (a number).
   *Why:* resource types aren't checked at build time. A number in `Padding` compiles fine and
   **crashes the app** when the view first appears (this happened in step 10).
4. **Use the components (§4) before building your own.** A tool window header is a
   `ToolWindowFrame`, an icon button is an `ActionButton`, an empty list is an `EmptyState`.
5. **Anything the user can *do* is an action (§5)**, so it appears in menus, Find Action
   and shortcuts automatically. Don't wire a button straight to a one-off command when it's
   a real user operation.
6. **MVVM as before:** ViewModels never touch controls, windows or `Application`; they use
   services (`IDialogService`, `IThemeService`, `IStatusService`). Code-behind contains only
   `InitializeComponent()`. View-only behavior goes into small reusable controls or attached
   properties (e.g. `FocusOnLoad`), not into code-behind.
7. **Accessible names.** Screen readers announce a list item's `ToString()` and a button's
   content. Give list-item ViewModels a `ToString()` that returns their label, and give
   icon-only or composite buttons `AutomationProperties.Name` (`ActionButton` does this for
   you when bound to an action).
8. **Settings sections are saved with `store.Update(s => s.Section = …)`**, never by
   saving an `AppSettings` you loaded earlier, which would overwrite other features' sections.
9. **Check it in the running app**, in Dark, Light and Compact, with long names and empty
   lists. Several bugs in UI-0 only showed up at runtime (see §8).
10. **Long operations show in the status bar, cancellable.** Wrap the work in
   `using var task = status.StartTask("Pushing main…", cts.Cancel)` and call
   `task.Report(step, percent)` as it goes (any thread); the bar shows the title, step, a
   progress bar (indeterminate when `percent` is null) and a Cancel button. Failures that need
   explaining go in a dialog: `new ConfirmDialogViewModel(title, explanation, "OK", details,
   destructive: false, showCancel: false)` is an OK-only message.

---

## 2. Where things are

The UI is split (plugin architecture, `docs/PLUGINS.md`): **DevHub.Sdk** holds what every UI
part builds on, **DevHub.App** is the shell, and each feature's screens live in its plugin's UI
project (`plugins/DevHub.Git/`, `plugins/DevHub.Projects/`, …) with the same folder layout
(`Actions/`, `ViewModels/…`, `Views/…`, `Themes/`). Namespaces are the same everywhere
(`DevHub.Controls`, `DevHub.Actions`, `DevHub.ViewModels.Git…`), whichever assembly a class is in.

```
src/DevHub.Sdk/                   shared by the shell and every plugin
  Themes/
    Tokens/             Colors.axaml (Dark+Light), Sizes.axaml (+ Sizes.Compact.axaml), Fonts.axaml
    DevHubTheme.axaml   merges the tokens, FluentMapping and component themes
    FluentMapping.axaml points Fluent's own resource keys at our tokens
    Controls.axaml      Styles: sizes for stock controls, text classes, shell styles
    Components/*.axaml  ControlThemes for our controls
  Controls/             our controls (Icon, ActionButton, ToolWindowFrame, …) + IconKind
  Converters/           value converters used in views
  Actions/              AppAction, IActionProvider, ActionRegistry, MenuBuilder, {a:…} XAML helpers
  Services/             IDialogService, IThemeService, IStatusService (interfaces only)
  ViewModels/           ViewModelBase, AvatarViewModel; DocumentViewModelBase + IDocumentFactory +
                        IDocumentService; ToolWindowViewModelBase; DialogViewModelBase +
                        ConfirmDialogViewModel; SettingsPageViewModelBase; Shell/ widget bases
  Views/Dialogs/        ConfirmDialogView
  Plugins/              IPlugin, PluginContext, [DevHubPlugin]
  Integration/          hooks between independent plugins (IProjectOpener, IProjectFileEditor, IProjectFolders)
  Assets/Icons/         JetBrains expui SVGs (light + _dark) + NOTICE.md / LICENSE-2.0.txt
  AppServices.cs        the DI container for XAML markup extensions (set by the app)

src/DevHub.App/                   the shell (no feature code)
  App.axaml(.cs)        theme includes, shell DI registrations, startup (plugins, keymap, save on close)
  Plugins/PluginHost.cs finds, resolves and loads the plugins
  ViewLocator.cs        XxxViewModel → XxxView, looked up in the ViewModel's own assembly
  Actions/              ActionIds (shell), Keymap, MainMenu (+ {a:MainMenu}, toolbar, Welcome); Providers/
  Services/             DialogService, ThemeService
  ViewModels/           DocumentManager, ToolWindowManager, Settings dialog + Appearance page,
                        status bar, Find Action, UI Gallery (Debug)
  Views/                same folders as ViewModels
  Controls/             DevHubLogo
  Assets/Logo/          app icon / logo

plugins/DevHub.Git/               a plugin UI project (the others look the same)
  GitPlugin.cs          the IPlugin: its info and every registration
  Actions/              GitActionIds, Providers/GitActionProvider
  ViewModels/  Views/   Git/, Documents/ (repository, diff, blame tabs), ToolWindows/ (Git Console)
  Services/  Controls/  RepositoryFileOpener, TerminalLauncher; CommitGraphCell
  Themes/GitTokens.axaml   Git-only tokens (diff / graph colors, list widths), from IPlugin.Resources
```

**XAML namespaces: use `using:`, not `clr-namespace:`** (`xmlns:c="using:DevHub.Controls"`).
`clr-namespace:X` without `;assembly=` only looks in the view's own assembly, so it can't
find the Sdk's types from the app (or a plugin); `using:` searches every referenced assembly.

The window, top to bottom: **title bar** (`Views/Shell/MainToolbar`: logo, ☰ main menu,
breadcrumb, toolbar, window buttons), the **work area** (stripes at the edges, tool-window
slots, the **editor area** with tabs or the Welcome view in the center), and the
**status bar**. The **Find Action** overlay sits on top while open.

---

## 3. Tokens

All in `Themes/Tokens/`. Use the `Brush.*` keys in views; the `Color.*` keys exist for
Fluent mapping and code.

### Colors (`Colors.axaml`)

| Key | Use for |
|---|---|
| `Brush.Bg.Editor` | editor area, text inputs, main content background |
| `Brush.Bg.Panel` | tool windows, title bar, status bar, dialogs |
| `Brush.Bg.Popup` | popups, menus, tooltips |
| `Brush.Bg.Hover` | hover highlight |
| `Brush.Bg.Selection` / `Brush.Bg.SelectionInactive` | selected row (focused / unfocused) |
| `Brush.Border` / `Brush.Border.Control` | panel dividers / control outlines |
| `Brush.Separator` | a line between two areas that are **both** on the editor color (in Dark, `Brush.Border` equals the editor color and vanishes there) |
| `Brush.Text.Primary` / `.Secondary` / `.Disabled` / `.OnAccent` | text |
| `Brush.Accent` (+ `.Hover`, `.Pressed`, `.Subtle`) | accent (user-configurable) |
| `Brush.Status.Error` / `.Warning` / `.Success` (+ `.Subtle`) | status colors; `.Subtle` = tinted backgrounds |

Every color exists in both the `Dark` and `Light` dictionaries; **add new colors to both**.

### Sizes (`Sizes.axaml`, overridden in `Sizes.Compact.axaml`)

| Kind | Keys |
|---|---|
| Heights/widths (number) | `Size.RowHeight`, `Size.ButtonHeight`, `Size.ToolbarHeight`, `Size.ToolWindowHeaderHeight`, `Size.StatusBarHeight`, `Size.TabHeight`, `Size.Icon` (16), `Size.StripeIcon` (20), `Size.ActionButton`, … |
| Gaps (number, for `Spacing`/`ColumnSpacing`) | `Spacing.Small` (4), `Spacing` (8), `Spacing.Large` (16), `Spacing.Toolbar` |
| Padding/Margin (Thickness) | `Padding.Button`, `Padding.TextBox`, `Padding.ListItem`, `Padding.Dialog`, `Padding.Document`, `Padding.Popup`, `Padding.SettingsRow`, … |
| Corners | `Radius.Control` (4), `Radius.Popup` (8), `Radius.Badge` |

Compact mode merges `Sizes.Compact.axaml` **after** `Sizes.axaml`; the later dictionary wins.
To make a size shrink in Compact, add the **same key** with a smaller value there.

### Fonts and text (`Fonts.axaml`, `Controls.axaml`)

Prefer the text classes over font tokens:

| Class | Look |
|---|---|
| *(none)* | body text, Inter 13 |
| `header` | 16, semibold |
| `secondary` | secondary color |
| `small` | 12 |
| `code` | JetBrains Mono (on `TextBlock` and `TextBox`) |

Classes combine: `<TextBlock Classes="small secondary" />`.

### Changing appearance at runtime

`IThemeService`: `Mode` (Dark / Light / System), `IsDark`, `IsCompact`, `SetAccent("#RRGGBB")`,
`Changed` event. It updates the resources (everything bound with `DynamicResource` follows)
**and saves the choice**. Never edit `Application.Resources` from elsewhere.

---

## 4. Components

Our controls (`Controls/*.cs`, looks in `Themes/Components/`). The UI Gallery shows each one.

| Component | Use | Example |
|---|---|---|
| `Icon` | a JetBrains icon; swaps light/dark files, fades when disabled | `<c:Icon Kind="folder" />` (names in `IconKind`) |
| `ActionButton` | square icon button; bind to an action for command + icon + tooltip | `<c:ActionButton Action="{a:Action Repositories.Rescan}" />` |
| `Toolbar` / `ToolbarSeparator` | row of action buttons | `<c:Toolbar><c:ActionButton …/><c:ToolbarSeparator/>…</c:Toolbar>` |
| `ToolWindowFrame` | tool window chrome: title + header actions + content | `<c:ToolWindowFrame Header="…"><c:ToolWindowFrame.HeaderActions>…` |
| `SearchField` | text box with search icon, clear ×, Esc clears | `<c:SearchField Text="{Binding Filter}" />` |
| `EmptyState` | centered "nothing here" with optional icon + action | `<c:EmptyState IconKind="folder" Title="…" Description="…"><Button …/></c:EmptyState>` |
| `SettingsRow` | label (+ description) column and an editor | `<c:SettingsRow Label="Theme" Description="…"><ComboBox …/></c:SettingsRow>` |
| `Badge` | small pill; classes `accent`, `success`, `warning`, `error` | `<c:Badge Classes="success" Content="clean" />` |
| `SplitterThumb` | drag handle resizing a bound size | used by the tool-window slots |
| `DevHubLogo` | the logo, accent-colored | `<c:DevHubLogo Width="32" Height="32" />` |
| `FocusOnLoad` | attached property: focus this element when it appears | `<TextBox c:FocusOnLoad.IsEnabled="True" />` |
| `DoubleTap` | attached property: run a command on double-click (rows in trees/lists) | `<Grid Background="Transparent" c:DoubleTap.Command="{Binding OpenCommand}" />` |
| `FocusRequest` | attached property: focus this element whenever a bound number goes up (ViewModel asks for focus) | `<TextBox c:FocusRequest.Token="{Binding Commit.FocusRequest}" />` |
| `CommitGraphCell` | one row of the commit graph, drawn in code from a Core `GraphRow` (colors `Brush.Graph.0…7`) | `<c:CommitGraphCell Row="{Binding Graph}" Lanes="{Binding GraphLanes}" LaneWidth="{DynamicResource Size.GraphLane}" />` |
| `Avatar` | a round account picture (bytes from a ViewModel), or the name's first letter on the accent color | `<c:Avatar ImageData="{Binding Avatar.Image}" Text="{Binding Author}" Width="{DynamicResource Size.AvatarSmall}" Height="{DynamicResource Size.AvatarSmall}" />` |
| `IconToggle` / `FoldArrow` (themes) | a small flat on/off icon button (e.g. "show folders"); a ▸ / ▾ fold arrow for custom tree rows | `<ToggleButton Theme="{StaticResource IconToggle}" IsChecked="{Binding ChangesAsTree}"><c:Icon Kind="folder" /></ToggleButton>` |

Git file lists share `Views/Git/FileChangeStyles.axaml` (change colors, the `FileRow` item
template, the `listGroupHeader` strip): `<StyleInclude Source="avares://DevHub.Git/Views/Git/FileChangeStyles.axaml" />` (the assembly name in the URI is the one the file lives in)
in the view's `Styles`, then `ItemTemplate="{StaticResource FileRow}"`.
A commit's (or stash's) message + files + diff is one reusable view: put a
`CommitDetailsViewModel` in a `ContentControl` and the ViewLocator shows `CommitDetailsView`
(All Commits and Stashes both do). List context menus also open with Shift+F10 / the Menu key,
so every right-click action is reachable from the keyboard (and from UI tests).

Stock Fluent controls (Button, TextBox, ComboBox, CheckBox, ListBox, TreeView, …) are
already restyled through `FluentMapping.axaml` and `Controls.axaml`; just use them.
`<Button Classes="accent">` is the primary (blue) button.

---

## 5. Recipes

### Add an icon
1. Copy `x.svg` and `x_dark.svg` from intellij-community `platform/icons/src/expui/…` (same
   pinned commit as in `Assets/Icons/NOTICE.md`) into `src/DevHub.Sdk/Assets/Icons/` (an icon only one plugin uses can live in that plugin; see `Icon.Kind`).
2. Add a constant to `Controls/IconKind.cs` (and to `IconKind.All`).
3. Add a row to the table in `Assets/Icons/NOTICE.md` (license requirement).

### Add a token
Shared tokens (used by the shell or several plugins) go in the Sdk's `Themes/Tokens/`. A token
only one plugin uses goes in that plugin's own dictionary (e.g. `plugins/DevHub.Git/Themes/GitTokens.axaml`,
listed in its `IPlugin.Resources`); colors there also need Dark and Light `ThemeDictionaries`.
Plugin dictionaries have no Compact variant yet, so a token that must shrink stays in the Sdk.
1. Add it to `Themes/Tokens/Sizes.axaml` (or `Colors.axaml`, in **both** Dark and Light).
2. If it should shrink in Compact, add the same key to `Sizes.Compact.axaml`.
3. Name it by purpose (`Padding.Tab`), not by look (`Padding12`). Check the type (rule 3).

### Add an action (a menu item / shortcut / Find Action entry)
Short version (full recipe in `ACTIONS_AND_FUZZY_MATCHING.md`):
1. Id constant ("Area.Verb") in `Actions/ActionIds.cs` (from P4: the plugin's own ids class).
2. `yield return new AppAction(id, "Text…", someViewModel.SomeCommand) { Category, IconKind, Gesture }`
   in the feature's `IActionProvider`. A new provider is registered in its **plugin's**
   `ConfigureServices` (e.g. `plugins/DevHub.Git/GitPlugin.cs`); shell actions in `App.axaml.cs`.
3. Say where it appears **on the action itself**: `Menu = "Git", MenuOrder = 100` (main-menu
   group; a new hundred starts a new section with a separator), `ToolbarOrder` (title-bar
   toolbar), `WelcomeOrder` (Welcome view). There's no list of ids to edit. In a view:
   `<c:ActionButton Action="{a:Action Id}" />` (hidden when the action's plugin is off), or
   `{a:ActionMenu 'id,-,id'}` for a context menu. Find Action and the shortcut work automatically.

### Add a document (an editor tab)
1. `ViewModels/Documents/FooViewModel.cs`: `public partial class FooViewModel : DocumentViewModelBase`
   with `Title` and `Icon`. (Its `Id`, saved in settings, defaults to "Foo".)
2. `Views/Documents/FooView.axaml` (+ `.axaml.cs` with only `InitializeComponent()`),
   `x:DataType="docs:FooViewModel"`. The ViewLocator finds it by name.
3. Register in the plugin's `ConfigureServices`: `services.AddSingleton<DocumentViewModelBase, FooViewModel>();`.
4. Done: `DocumentActionProvider` creates `Navigate.Foo` ("Foo"), so it's in Find Action. For a
   menu entry, label, shortcut or Welcome link, override `Navigation`:
   `public override DocumentNavigation Navigation => new() { Text = "New Foo…", Gesture = "Ctrl+N", Menu = "File", MenuOrder = 0, WelcomeOrder = 10 };`

Documents are singletons: closing and reopening a tab keeps its state. The content area
already has `Padding.Document` around it.

### Add a multi-instance document (one tab per *thing*, e.g. per repository)
For documents that can be open several times, each for a different item. Example in the
code: `RepositoryDocumentViewModel` + `RepositoryDocumentFactory`.
1. ViewModel: subclass `DocumentViewModelBase`; override `Id` with a prefix + the item's key
   (`"Repository:" + path`), which is how tabs are found, saved and restored. Implement
   `IDisposable` if it holds anything to release; the manager disposes it when its tab closes.
   Heavy work goes in `OnActivationChanged(bool isActive)`, not the constructor, so tabs
   restored in the background stay idle. (Repository tabs are the exception: one cheap
   `git status` at creation gives every tab its dirty dot.) Set `IsDirty` to show the
   "modified" dot on the tab. A section with its own ViewModel (e.g. `LocalChangesViewModel`)
   is shown through a `ContentControl` (ViewLocator), with a local `DataTemplate` for plain
   placeholder records.
2. Factory: implement `IDocumentFactory` (`IdPrefix`, `Create(id)` → the document, or null
   if the item is gone). Register: `services.AddSingleton<IDocumentFactory, FooFactory>();`.
3. Open it with `documentManager.OpenById(FooFactory.IdFor(item))`: activates the existing tab
   for that item, or creates one. Restoring at startup works automatically; ids that can't
   be recreated are skipped with a status-bar note. From inside another document, call
   `OpenDocument?.Invoke(id)` instead (documents can't inject the `DocumentManager`, it's built
   from them). Example: a repository tab opening a diff tab (`DiffDocumentFactory`, ids like
   `Diff:unstaged|<repo>|<file>`).

### Add a tool window (a docked panel)
1. `ViewModels/ToolWindows/FooViewModel.cs`: subclass `ToolWindowViewModelBase` with
   `Id` (saved; don't rename), `Title`, `IconKind`, `Slot` (Left / Right / Bottom).
2. `Views/ToolWindows/FooView.axaml`: wrap the content in a `ToolWindowFrame` with a
   `Toolbar` of `ActionButton`s in `HeaderActions`, and a hide button
   (`<c:ActionButton IconKind="close" ToolTip.Tip="Hide" Command="{Binding ToggleCommand}" />`).
3. Register it twice in the plugin's `ConfigureServices` (as itself, for providers, and as the base type, for the manager):
   ```csharp
   services.AddSingleton<FooViewModel>();
   services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<FooViewModel>());
   ```
4. Done: it gets a stripe button, a slot, a saved size, a View-menu entry and an Alt+N action
   (`ToolWindowActionProvider`, numbered by `Order`; override `Order`, and `WelcomeOrder` for a Welcome link).

### Add a dialog
1. ViewModel: subclass `DialogViewModelBase` (`Title`, optional `Width`/`Height`,
   `ShowApply`). Override `CanApply()` and `ApplyChangesAsync()` (return false to keep the
   dialog open). The base class provides OK / Cancel / Apply; Enter = OK, Esc = Cancel.
2. View: `Views/Dialogs/FooDialogView.axaml`, only the body (the footer comes from
   `DialogWindow`).
3. Register as **transient** (a fresh dialog each time) plus a factory if an action opens it:
   ```csharp
   services.AddTransient<FooDialogViewModel>();
   services.AddSingleton<Func<FooDialogViewModel>>(sp => () => sp.GetRequiredService<FooDialogViewModel>());
   ```
4. Open it: `await dialogs.ShowDialogAsync(createDialog())` (returns true for OK).

**Confirming a destructive operation** needs no new dialog: use `ConfirmDialogViewModel`
(message, the OK label as a verb, the affected items). `IsDestructive` turns OK red (`Button.accent.danger`).
Say exactly what will be lost, and only the sentences that apply:
```csharp
var ok = await dialogs.ShowDialogAsync(new ConfirmDialogViewModel(
    "Discard Changes", "Delete src/New.cs? It isn't tracked by git, so it is deleted permanently.",
    "Discard", ["src/New.cs"]));
```
For text actions inside a pane header ("Stage All"), use `<Button Classes="flat" />`.

### Add a Settings page
1. `ViewModels/Dialogs/Settings/FooSettingsPageViewModel.cs`: subclass
   `SettingsPageViewModelBase` (`Title`, `Keywords` for the search box). Load the current
   values in the constructor (into backing fields, so loading isn't an "edit"), call
   `MarkModified()` when the user changes something, and save in `Apply()` with
   `store.UpdateSection<FooSettings>(...)` (a plugin's section) or `store.Update(...)` (shell).
   Override `Order` for its place in the list.
2. View `Views/Dialogs/Settings/FooSettingsPageView.axaml`: a stack of `SettingsRow`s.
3. Register in the plugin's `ConfigureServices` as **transient** under the base type:
   `services.AddTransient<SettingsPageViewModelBase, FooSettingsPageViewModel>();`. The dialog
   receives every page that way (fresh ones each time it opens).

### Add a title-bar or status-bar widget
1. ViewModel: subclass `TitleBarWidgetViewModelBase` or `StatusBarWidgetViewModelBase`
   (`Order` for its position); a View next to it as usual. Example: Git's `BranchWidgetViewModel`.
2. Register in the plugin: `services.AddSingleton<TitleBarWidgetViewModelBase, FooWidgetViewModel>();`.
   Title-bar widgets need `WindowDecorationProperties.ElementRole="User"` on clickable parts.

### Persist something new
**A plugin's setting** (almost everything): add a property to the plugin's settings section
(a class implementing `ISettingsSection` in its `.Core` project, e.g. `GitSettings`,
`ProjectsSettings`; create one if the plugin has none). Read with
`store.LoadSection<GitSettings>()`, save with `store.UpdateSection<GitSettings>(s => s.Foo = …)`,
and add a round-trip test in that plugin's tests. **A shell setting** (window layout,
appearance): a property on `AppSettings` in `src/DevHub.Sdk.Core/Settings`, nullable if
"never saved" must be distinguishable from a default, saved with `store.Update(s => s.Foo = …)`,
tested in `JsonSettingsStoreTests`. For experiments, run the app with
`DEVHUB_DATA_DIR` pointing at a scratch folder so your real settings stay untouched.

---

## 6. Layout tips

- **Rows that must fit** (icon + text + something on the right): a `Grid` with
  `ColumnDefinitions="Auto,*,Auto"` and `TextTrimming="CharacterEllipsis"` on the text.
  A horizontal `StackPanel` gives its children unlimited width, so text never trims and
  overlaps instead.
- **Input + button** (path + Browse…): `Grid ColumnDefinitions="*,Auto"`, not a fixed-width
  TextBox; fixed widths get cropped in narrow places.
- **Vertical lists of controls:** `StackPanel Spacing="{DynamicResource Spacing}"`.
- **Comma selectors** in styles (`Selector="TextBox, ComboBox"`) are typed as the controls'
  common base class, so properties that only one has fail to compile. Write one style per type.
- **Thicknesses can't contain a resource:** `Margin="0,0,{DynamicResource X},0"` is invalid.
  Make a Thickness token, or use an empty spacer column.

---

## 7. Styles vs ControlThemes (how theming works)

- A **ControlTheme** is a control's template + defaults + state styles (`^:pointerover`),
  keyed by type. Ours are in `Themes/Components/`. Fluent's are the defaults for stock controls.
- A **Style** (`Themes/Controls.axaml`, `<X.Styles>`) adjusts existing controls. Styles beat
  ControlTheme setters, which is how we restyle Fluent without rewriting its templates.
- **TemplateBinding beats styles** inside a template, so a style can't override a value a
  template sets with TemplateBinding (e.g. the drawn title text's visibility); target another
  property (Opacity) instead.
- **Fluent's colors** come from its resource keys (`ButtonBackground`, …), which
  `FluentMapping.axaml` redefines in terms of our tokens. The accent family
  (`SystemAccentColor…`) is set in code by `ThemeService`.

---

## 8. Avalonia 12 gotchas we hit

| Symptom | Cause | Fix used |
|---|---|---|
| App crashes when a view opens: `InvalidCastException Double → Thickness` | a number token in `Padding` | use a Thickness token (rule 3) |
| `Alt+1` shortcut never fires | `KeyGesture.Parse("Alt+1")` → `Key.Cancel` (enum value 1) | always `Gestures.Parse` |
| Disabled SVG icon not faded | Svg.Skia ignores `Opacity` | `Icon` injects CSS opacity when disabled |
| Fullscreen caption button over our toolbar; hiding it shifted min/max/close | Avalonia 12 draws 4 caption buttons; `IsVisible=False` left a gap | collapse it to width 0 (`Controls.axaml`) |
| Closing a tab left nothing selected | a two-way `SelectedItem` binding nulls the property when the item is removed | remember "was active" before removing |
| Button text not centered | Fluent leaves `HorizontalContentAlignment` at Stretch | global Button style centers it |
| TextBox inner icon/× against the border | Fluent applies `Padding` only to the text column | `Margin.InnerLeft/Right` tokens |
| Screen readers / UI tests see `TreeView` rows without a name | unlike `ListBoxItem`, a `TreeViewItem` doesn't use the item's `ToString()` | style: `<Style Selector="TreeViewItem" x:DataType="…"><Setter Property="AutomationProperties.Name" Value="{Binding AccessibleName}" /></Style>` |
| Multi-line test strings suddenly contain `\r` | raw / verbatim literals take the source file's line endings, and `core.autocrlf` checks files out as CRLF | normalise in the test (`text.ReplaceLineEndings("\n")`) instead of relying on the checkout |
| App crashes at startup: `XamlLoadException: No precompiled XAML found for DevHub.App` | a build failed half-way (e.g. a locked `obj` DLL while Rider was building) and the next build reused the DLL without its compiled XAML | rebuild with `--no-incremental` |
| Rows that should be selectable but keep their own look (diff lines) | a `ListBox` adds row height and padding, and a row's own background hides the selection | `ListBox.x > ListBoxItem { MinHeight: 0; Padding: 0 }`, plus a `:selected` style for the row's Border declared after its color styles |
| App crashes at startup: `ArgumentException: An item with the same key has already been added. Key: Size.X` | two tokens with the same key (the build doesn't check) | search `Sizes.axaml` for the key before adding one |
| A `*` column in a list row is empty | the fixed columns are wider than the row; or the ListBox scrolls horizontally, so rows get infinite width | `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` on the list; `MinWidth` on the `*` column (`StaticResource`: a `ColumnDefinition` can't use `DynamicResource`) |
| Screen readers say "Avalonia.Controls.Grid" | no accessible name; falls back to content type / `ToString()` | `ToString()` / `AutomationProperties.Name` (rule 7) |
| Settings written by one feature lost after another saves | saving a stale `AppSettings` copy | `store.Update` (rule 8) |
| `Watermark` obsolete warning | renamed in 12 | `PlaceholderText` |
| `Bitmap.Save(stream)` obsolete | new overload | `Save(stream, PngBitmapEncoderOptions.Default)` |
| `Svg` is "a namespace but used like a type" | package namespace clash | `using SvgControl = Avalonia.Svg.Skia.Svg;` |
| Settings ignore the `APPDATA` variable | `GetFolderPath` asks Windows directly | `DEVHUB_DATA_DIR` override |
| Fuzzy search too loose ("rider" matched Appearance) | matching one long keyword string lets letters scatter | match title and each keyword separately |

---

## 9. Checklist before you're done

- [ ] No hex / pixel / font values in the view; tokens via `DynamicResource`, right types.
- [ ] Looks right in **Dark**, **Light** and **Compact** (UI Gallery switches, or Settings).
- [ ] Long names trim, empty lists show an `EmptyState`.
- [ ] User operations are actions (menu / Find Action / shortcut).
- [ ] Icon-only buttons and list items have accessible names.
- [ ] Ran the app and used the feature; no errors in the output.
- [ ] Settings changes saved with `store.Update`, with a round-trip test.
