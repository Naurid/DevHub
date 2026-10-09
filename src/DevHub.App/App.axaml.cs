using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevHub.Actions;
using DevHub.Actions.Providers;
using DevHub.Controls;
using DevHub.Core.Projects;
using DevHub.Core.Repositories;
using DevHub.Core.Settings;
using DevHub.Services;
using DevHub.ViewModels;
using DevHub.ViewModels.Pages;
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
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            Services = collection.BuildServiceProvider();

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

            // Remember which tool windows were open, and their sizes, for next launch.
            mainWindow.Closing += (_, _) => Services.GetRequiredService<ToolWindowManager>().Save();

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
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

        // Core services.
        // DEVHUB_DATA_DIR (optional) points settings at another folder, e.g. to try things or
        // run a test copy without touching your real %APPDATA%\DevHub\settings.json.
        services.AddSingleton<ISettingsStore>(_ =>
            new JsonSettingsStore(Environment.GetEnvironmentVariable("DEVHUB_DATA_DIR") is { Length: > 0 } dir ? dir : null));
        services.AddSingleton<IToolLocator, ToolLocator>();
        services.AddSingleton<IProjectLanguageDetector, ProjectLanguageDetector>();
        services.AddSingleton<IProjectLauncher, ProjectLauncher>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();

        // Tool windows: registered as themselves (for providers) and as the base type, so the
        // ToolWindowManager receives all of them as IEnumerable<ToolWindowViewModelBase>.
        services.AddSingleton<RepositoriesViewModel>();
        services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<RepositoriesViewModel>());
        services.AddSingleton<ToolWindowManager>();

        // Page view-models.
        services.AddSingleton<TemplatesViewModel>();
        services.AddSingleton<GenerateViewModel>();
        services.AddSingleton<ImportViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<UiGalleryViewModel>();

        // Shell.
        services.AddSingleton<MainWindowViewModel>();

        // Actions: each provider contributes some; the registry collects them all
        // (DI passes every registered IActionProvider as IEnumerable<IActionProvider>).
        services.AddSingleton<IActionProvider, ViewActionProvider>();
        services.AddSingleton<IActionProvider, RepositoryActionProvider>();
        services.AddSingleton<IActionProvider, ToolWindowActionProvider>();
        services.AddSingleton<ActionRegistry>();
    }
}
