using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevHub.Actions;
using DevHub.Actions.Providers;
using DevHub.Controls;
using DevHub.Core.Git;
using DevHub.Core.Projects;
using DevHub.Core.Repositories;
using DevHub.Core.Settings;
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

            // Remember the open tabs and tool windows (and their sizes) for next launch.
            mainWindow.Closing += (_, _) =>
            {
                Services.GetRequiredService<DocumentManager>().Save();
                Services.GetRequiredService<ToolWindowManager>().Save();
            };

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
        services.AddSingleton<ExternalEditor>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();

        // Git: one shared command log and runner. The runner is only registered when git is
        // installed (and new enough); without it, repository tabs show how to install Git.
        services.AddSingleton<IGitCommandLog, GitCommandLog>();
        if (GitExecutable.TryLocate() is { } git)
        {
            services.AddSingleton(git);
            services.AddSingleton<GitRunner>(sp => new GitRunner(git, sp.GetRequiredService<IGitCommandLog>()));
        }

        // Tool windows: registered as themselves (for providers) and as the base type, so the
        // ToolWindowManager receives all of them as IEnumerable<ToolWindowViewModelBase>.
        services.AddSingleton<RepositoriesViewModel>();
        services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<RepositoriesViewModel>());
        services.AddSingleton<GitConsoleViewModel>();
        services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<GitConsoleViewModel>());
        services.AddSingleton<ToolWindowManager>();

        // Documents (editor tabs): each registered as the base type so the DocumentManager
        // receives them all as IEnumerable<DocumentViewModelBase>; the order is the menu order.
        services.AddSingleton<DocumentViewModelBase, TemplatesViewModel>();
        services.AddSingleton<DocumentViewModelBase, GenerateViewModel>();
        services.AddSingleton<DocumentViewModelBase, ImportViewModel>();
#if DEBUG
        // Design-system playground; not shipped in Release builds.
        services.AddSingleton<DocumentViewModelBase, UiGalleryViewModel>();
#endif
        // Instance documents: one tab per repository, created on demand / restored by id.
        services.AddSingleton<IDocumentFactory, RepositoryDocumentFactory>();
        services.AddSingleton<IDocumentFactory, DiffDocumentFactory>();
        services.AddSingleton<DocumentManager>();

        // Dialogs: transient = a fresh instance every time one is requested, so each opening
        // of Settings starts from the saved values. The Func is a tiny factory for the action.
        services.AddTransient<AppearanceSettingsPageViewModel>();
        services.AddTransient<OpenWithSettingsPageViewModel>();
        services.AddTransient<SettingsDialogViewModel>();
        services.AddSingleton<Func<SettingsDialogViewModel>>(sp => () => sp.GetRequiredService<SettingsDialogViewModel>());
        services.AddTransient<FindActionViewModel>();
        services.AddSingleton<Func<FindActionViewModel>>(sp => () => sp.GetRequiredService<FindActionViewModel>());

        // Shell.
        services.AddSingleton<MainWindowViewModel>();

        // Actions: each provider contributes some; the registry collects them all
        // (DI passes every registered IActionProvider as IEnumerable<IActionProvider>).
        services.AddSingleton<IActionProvider, ViewActionProvider>();
        services.AddSingleton<IActionProvider, RepositoryActionProvider>();
        services.AddSingleton<IActionProvider, ToolWindowActionProvider>();
        services.AddSingleton<IActionProvider, DocumentActionProvider>();
        services.AddSingleton<IActionProvider, SettingsActionProvider>();
        services.AddSingleton<IActionProvider, FindActionProvider>();
        services.AddSingleton<IActionProvider, GitActionProvider>();
        services.AddSingleton<ActionRegistry>();
    }
}
