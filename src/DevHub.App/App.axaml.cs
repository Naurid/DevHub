using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using DevHub.Actions;
using DevHub.Actions.Providers;
using DevHub.Controls;
using DevHub.Plugins;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.ViewModels;
using DevHub.ViewModels.Dialogs.Settings;
using DevHub.ViewModels.Documents;
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
            var plugins = PluginHost.Load(store);
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

            // Remember the open tabs and tool windows (and their sizes) for next launch.
            mainWindow.Closing += (_, _) =>
            {
                Services.GetRequiredService<DocumentManager>().Save();
                Services.GetRequiredService<ToolWindowManager>().Save();
            };

            // A plugin that was switched on but couldn't load (e.g. its requirement is missing).
            if (plugins.Summary() is { } summary)
                Services.GetRequiredService<IStatusService>().Show(summary);

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// The shell's own services: window, tabs, tool-window frame, menus, Settings, Find Action.
    /// Every feature registers itself in its plugin (Plugins/FeaturePlugins.cs).
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
        services.AddSingleton<ActionRegistry>();
    }
}
