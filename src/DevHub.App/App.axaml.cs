using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using DevHub.Actions;
using DevHub.Actions.Providers;
using DevHub.Controls;
using DevHub.Plugins;
using DevHub.Sdk.Core.Plugins.Marketplace;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.ViewModels;
using DevHub.ViewModels.Dialogs;
using DevHub.ViewModels.Dialogs.Settings;
using DevHub.ViewModels.Documents;
using DevHub.ViewModels.Plugins;
using DevHub.ViewModels.Shell;
using DevHub.ViewModels.ToolWindows;
using DevHub.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DevHub;

public partial class App : Application
{
    /// <summary>The composition root. Resolved once the lifetime is known.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // DEVHUB_DATA_DIR (optional) points settings at another folder, e.g. to try things or
            // run a test copy without touching your real %APPDATA%\DevHub\settings.json.
            var store = new JsonSettingsStore(Environment.GetEnvironmentVariable("DEVHUB_DATA_DIR") is { Length: > 0 } dir ? dir : null);

            // Which plugins load is decided before the container exists: a switched-off plugin
            // registers nothing, so no menu item, tab, tool window or shortcut of it appears.
            var dataDir = Environment.GetEnvironmentVariable("DEVHUB_DATA_DIR") is { Length: > 0 } data ? data : null;
            var plugins = PluginHost.Load(store, PluginInstaller.DefaultRoot(dataDir));
#if DEBUG
            // One line for whoever starts the app from a console or script (stdout; a WinExe has no window for it).
            Console.WriteLine("DevHub plugins: found " + string.Join(", ", plugins.Available.Select(p => p.Info.Id))
                              + "; loaded " + string.Join(", ", plugins.Loaded.Select(p => p.Info.Id))
                              + (plugins.Skipped.Count > 0 ? "; skipped " + string.Join(", ", plugins.Skipped.Select(s => s.Plugin.Id)) : ""));
#endif

            var collection = new ServiceCollection();
            ConfigureServices(collection, store, plugins);
            plugins.ConfigureServices(collection, store);
#if DEBUG
            // Check every registration can be built now, not when it's first used: with plugins
            // switched on and off, a missing dependency would otherwise only fail on some click.
            Services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
#else
            Services = collection.BuildServiceProvider();
#endif
            // XAML markup extensions ({a:Action …}, in the Sdk and in plugins) read the container from here.
            AppServices.Current = Services;

            // The plugins' own resources (design tokens, styles), before any window exists.
            foreach (var uri in plugins.Resources)
                Resources.MergedDictionaries.Add(new ResourceInclude(uri) { Source = uri });

            // Push the accent into Fluent's keys before any window is created.
            var theme = Services.GetRequiredService<ThemeService>();
            theme.Initialize();

            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
                Icon = DevHubLogo.CreateWindowIcon(theme.AccentColor)
            };

            // Title bar / taskbar icon is a bitmap, so re-render it when the appearance changes.
            void UpdateWindowIcon(object? sender, EventArgs e) =>
                mainWindow.Icon = DevHubLogo.CreateWindowIcon(theme.AccentColor);
            theme.Changed += UpdateWindowIcon;
            mainWindow.Closed += (_, _) => theme.Changed -= UpdateWindowIcon;

            // The dialog service needs a TopLevel to drive pickers; hand it the window.
            Services.GetRequiredService<DialogService>().Attach(mainWindow);

            // Window-wide keyboard shortcuts for every action that has one.
            Keymap.Apply(Services.GetRequiredService<ActionRegistry>(), mainWindow);

            // Unsaved edits in a tab (a template editor): ask before they're lost. The first close is
            // cancelled while the question is open; agreeing closes the window again, this time for real.
            var discardAgreed = false;
            mainWindow.Closing += async (_, e) =>
            {
                var documents = Services.GetRequiredService<DocumentManager>();
                var unsaved = documents.Open.Where(d => d.HasUnsavedChanges).Select(d => d.Title).ToList();
                if (discardAgreed || unsaved.Count == 0)
                    return;
                e.Cancel = true;
                var discard = await Services.GetRequiredService<IDialogService>().ShowDialogAsync(new ConfirmDialogViewModel(
                    "Unsaved Changes", "These tabs have changes that aren't saved. Close DevHub and lose them?",
                    "Discard and Close", unsaved));
                if (!discard)
                    return;
                discardAgreed = true;
                mainWindow.Close();
            };

            // Remember the open tabs and tool windows (and their sizes) for next launch.
            mainWindow.Closing += (_, e) =>
            {
                if (e.Cancel)
                    return;
                Services.GetRequiredService<DocumentManager>().Save();
                Services.GetRequiredService<ToolWindowManager>().Save();
            };

            // A plugin that was switched on but couldn't load (e.g. its requirement is missing).
            if (plugins.Summary() is { } summary)
                Services.GetRequiredService<IStatusService>().Show(summary);

            // First start (no plugin choice saved yet): ask what to use, over the main window.
            if (store.Load().EnabledPlugins is null)
            {
                async void AskOnce(object? sender, EventArgs e)
                {
                    mainWindow.Opened -= AskOnce;
                    try
                    {
                        await AskForPluginsAsync(plugins, store);
                    }
                    catch (Exception ex)
                    {
                        // An event handler can't pass this on: say it, and keep the app running.
                        Services.GetRequiredService<IStatusService>().Show("Couldn't save the plugin choice: " + ex.Message);
                    }
                }
                mainWindow.Opened += AskOnce;
            }

            // A newer DevHub? Asked quietly a few seconds after start (Settings → Updates); the status bar
            // offers it, nothing installs without a click.
            if (store.Load().Updates?.CheckOnStartup ?? true)
                _ = CheckForUpdatesSoonAsync(Services.GetRequiredService<UpdateService>());

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task CheckForUpdatesSoonAsync(UpdateService updates)
    {
        await Task.Delay(TimeSpan.FromSeconds(5)); // not while the window is still coming up
        await updates.CheckAsync(); // never throws: a failed check just isn't news
    }

    /// <summary>
    /// The first-run picker. Its choice is saved; if it differs from what loaded (everything, on a
    /// first start), DevHub restarts with it. Closing it with the window's X keeps everything (and
    /// saves that, so it isn't asked again; Settings → Plugins changes it later).
    /// </summary>
    private static async Task AskForPluginsAsync(PluginHost plugins, ISettingsStore store)
    {
        var picker = new FirstRunViewModel(plugins, store);
        if (!await Services.GetRequiredService<IDialogService>().ShowDialogAsync(picker))
        {
            var all = plugins.Loaded.Select(p => p.Info.Id).ToList();
            store.Update(s => s.EnabledPlugins = all);
            return;
        }
        if (picker.Choice.RestartNeeded)
            Services.GetRequiredService<IAppRestarter>().Restart();
    }

    /// <summary>
    /// The shell's own services: window, tabs, tool-window frame, menus, Settings, Find Action,
    /// the Plugins page. Every feature registers itself in its plugin class (plugins/DevHub.*).
    /// </summary>
    private static void ConfigureServices(IServiceCollection services, ISettingsStore store, PluginHost plugins)
    {
        // Services (UI seams the ViewModels talk to instead of touching Avalonia).
        // Registered once; exposed both as the concrete type (for App to Attach the window)
        // and as the interface (what ViewModels depend on).
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());

        // The status bar ViewModel is also the status service (same instance for both).
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<IStatusService>(sp => sp.GetRequiredService<StatusBarViewModel>());

        services.AddSingleton(store);
        services.AddSingleton(plugins);

        // Tool windows (from plugins, registered as ToolWindowViewModelBase) and editor tabs
        // (DocumentViewModelBase singletons, IDocumentFactory for one tab per id).
        services.AddSingleton<ToolWindowManager>();
        services.AddSingleton<DocumentManager>();
        services.AddSingleton<IDocumentService>(sp => sp.GetRequiredService<DocumentManager>());
#if DEBUG
        // Design-system playground; not shipped in Release builds.
        services.AddSingleton<DocumentViewModelBase, UiGalleryViewModel>();
#endif

        // Dialogs: transient = a fresh instance every time one is requested, so each opening
        // of Settings starts from the saved values. The Func is a tiny factory for the action.
        services.AddTransient<SettingsPageViewModelBase, AppearanceSettingsPageViewModel>();
        services.AddTransient<SettingsPageViewModelBase, PluginsSettingsPageViewModel>();
        services.AddTransient<SettingsPageViewModelBase, UpdatesSettingsPageViewModel>();

        // DevHub's own updates (GitHub Releases, Velopack): the service, its status-bar note.
        services.AddSingleton<UpdateService>();
        services.AddSingleton<StatusBarWidgetViewModelBase, UpdateWidgetViewModel>();
        services.AddSingleton<IAppRestarter, AppRestarter>();
        services.AddTransient<SettingsDialogViewModel>();
        services.AddSingleton<Func<SettingsDialogViewModel>>(sp => () => sp.GetRequiredService<SettingsDialogViewModel>());
        services.AddTransient<FindActionViewModel>();
        services.AddSingleton<Func<FindActionViewModel>>(sp => () => sp.GetRequiredService<FindActionViewModel>());

        // Shell.
        services.AddSingleton<MainWindowViewModel>();

        // Actions: each provider contributes some (plugins add theirs); the registry collects
        // them all (DI passes every registered IActionProvider as IEnumerable<IActionProvider>).
        services.AddSingleton<IActionProvider, ViewActionProvider>();
        services.AddSingleton<IActionProvider, ToolWindowActionProvider>();
        services.AddSingleton<IActionProvider, DocumentActionProvider>();
        services.AddSingleton<IActionProvider, SettingsActionProvider>();
        services.AddSingleton<IActionProvider, FindActionProvider>();
        services.AddSingleton<IActionProvider, RestartActionProvider>();
        services.AddSingleton<IActionProvider, UpdateActionProvider>();
        services.AddSingleton<ActionRegistry>();
    }
}
