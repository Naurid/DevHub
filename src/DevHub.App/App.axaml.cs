using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevHub.Controls;
using DevHub.Core.Projects;
using DevHub.Core.Repositories;
using DevHub.Core.Settings;
using DevHub.Services;
using DevHub.ViewModels;
using DevHub.ViewModels.Pages;
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

            // Title bar / taskbar icon is a bitmap, so re-render it when the accent changes.
            void UpdateWindowIcon(object? sender, EventArgs e) =>
                mainWindow.Icon = DevHubLogo.CreateWindowIcon(theme.AccentColor);
            theme.AccentChanged += UpdateWindowIcon;
            mainWindow.Closed += (_, _) => theme.AccentChanged -= UpdateWindowIcon;

            // The dialog service needs a TopLevel to drive pickers; hand it the window.
            Services.GetRequiredService<DialogService>().Attach(mainWindow);

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

        // Core services.
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore());
        services.AddSingleton<IToolLocator, ToolLocator>();
        services.AddSingleton<IProjectLanguageDetector, ProjectLanguageDetector>();
        services.AddSingleton<IProjectLauncher, ProjectLauncher>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();

        // Page view-models.
        services.AddSingleton<RepositoriesViewModel>();
        services.AddSingleton<TemplatesViewModel>();
        services.AddSingleton<GenerateViewModel>();
        services.AddSingleton<ImportViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<UiGalleryViewModel>();

        // Shell.
        services.AddSingleton<MainWindowViewModel>();
    }
}
