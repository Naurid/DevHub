using DevHub.Actions;
using DevHub.Actions.Providers;
using DevHub.Git.Core;
using DevHub.GitHub.Core;
using DevHub.Projects.Core;
using DevHub.Sdk.Core.Plugins;
using DevHub.Services;
using DevHub.ViewModels.Dialogs.Settings;
using DevHub.ViewModels.Documents;
using DevHub.ViewModels.Git;
using DevHub.ViewModels.Shell;
using DevHub.ViewModels.ToolWindows;
using Microsoft.Extensions.DependencyInjection;

// The features that ship with DevHub. Each is a plugin like any other: the host finds them
// through these attributes and only calls the ones that are switched on. (Their code still lives
// in this project; P4 moves each into its own assembly next to its .Core project.)
[assembly: DevHub.Plugins.DevHubPlugin(typeof(DevHub.Plugins.ProjectsPlugin))]
[assembly: DevHub.Plugins.DevHubPlugin(typeof(DevHub.Plugins.GitPlugin))]
[assembly: DevHub.Plugins.DevHubPlugin(typeof(DevHub.Plugins.GitHubPlugin))]
[assembly: DevHub.Plugins.DevHubPlugin(typeof(DevHub.Plugins.TemplatesPlugin))]

namespace DevHub.Plugins;

/// <summary>Projects: folders to scan, the repositories in them, their languages, Open With.</summary>
public sealed class ProjectsPlugin : IPlugin
{
    public const string Id = "devhub.projects";

    public PluginInfo Info { get; } = new(Id, "Projects",
        "The Projects tool window: choose folders, find the git repositories in them, see their language and open them in the right tool.",
        []) { Category = "Projects" };

    public void ConfigureServices(IServiceCollection services, PluginContext context)
    {
        services.AddSingleton<IToolLocator, ToolLocator>();
        services.AddSingleton<IProjectLanguageDetector, ProjectLanguageDetector>();
        services.AddSingleton<IProjectLauncher, ProjectLauncher>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
        // Used by plugins that require Projects (Git opens files and terminals with them).
        services.AddSingleton<ExternalEditor>();
        services.AddSingleton<TerminalLauncher>();

        services.AddSingleton<RepositoriesViewModel>();
        services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<RepositoriesViewModel>());
        services.AddSingleton<IActionProvider, RepositoryActionProvider>();
        services.AddTransient<SettingsPageViewModelBase, OpenWithSettingsPageViewModel>();
    }
}

/// <summary>Git: the Fork-style Git client (repository, diff and blame tabs, Git Console, branch widget).</summary>
public sealed class GitPlugin : IPlugin
{
    public const string Id = "devhub.git";

    /// <summary>git.exe, looked up once (null when git isn't installed or too old). GitHub uses it too.</summary>
    internal static readonly Lazy<GitExecutable?> Executable = new(GitExecutable.TryLocate);

    public PluginInfo Info { get; } = new(Id, "Git",
        "A Fork-style Git client: changes, staging per line, commits, history graph, branches, remotes, merge and rebase, stashes, blame.",
        [ProjectsPlugin.Id]) { Category = "Version control" };

    public void ConfigureServices(IServiceCollection services, PluginContext context)
    {
        // One shared command log and runner. The runner is only registered when git is installed
        // (and new enough); without it, repository tabs show how to install Git.
        services.AddSingleton<IGitCommandLog, GitCommandLog>();
        if (Executable.Value is { } git)
        {
            services.AddSingleton(git);
            services.AddSingleton<GitRunner>(sp => new GitRunner(git, sp.GetRequiredService<IGitCommandLog>()));
        }

        services.AddSingleton<GitConsoleViewModel>();
        services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<GitConsoleViewModel>());

        // One tab per repository, plus diff and blame tabs, created on demand / restored by id.
        // The layout (panel widths, tree / flat, unified / side-by-side) is shared by all of them.
        services.AddSingleton(sp => new RepositoryTabLayout(sp.GetRequiredService<DevHub.Sdk.Core.Settings.ISettingsStore>()));
        services.AddSingleton<RepositoryTabServices>();
        services.AddSingleton<IDocumentFactory, RepositoryDocumentFactory>();
        services.AddSingleton<IDocumentFactory, DiffDocumentFactory>();
        services.AddSingleton<IDocumentFactory, BlameDocumentFactory>();
        services.AddSingleton<CloneOperations>();

        services.AddSingleton<ActiveRepositoryService>();
        services.AddSingleton<TitleBarWidgetViewModelBase, BranchWidgetViewModel>();
        // Soft hook of Projects: double-click a project → its repository tab.
        services.AddSingleton<IProjectOpener, GitProjectOpener>();
        services.AddSingleton<IActionProvider, GitActionProvider>();
    }
}

/// <summary>GitHub: the account (through Git Credential Manager), Publish to GitHub, your repositories in Clone.</summary>
public sealed class GitHubPlugin : IPlugin
{
    public const string Id = "devhub.github";

    public PluginInfo Info { get; } = new(Id, "GitHub",
        "Sign in to GitHub through Git Credential Manager, publish a repository to your account, clone your repositories, show avatars.",
        [GitPlugin.Id]) { Category = "Version control" };

    public void ConfigureServices(IServiceCollection services, PluginContext context)
    {
        // The account comes from Git Credential Manager (git), so the API needs git too.
        if (GitPlugin.Executable.Value is not null)
        {
            services.AddSingleton<GitCredentialManager>();
            services.AddSingleton(new GitHubApi(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));
            services.AddSingleton<GitHubService>();
        }
        services.AddSingleton<GitHubSession>();
        services.AddTransient<SettingsPageViewModelBase, GitHubSettingsPageViewModel>();
    }
}

/// <summary>Templates: project and file templates (placeholder pages until milestone M1 builds the real thing).</summary>
public sealed class TemplatesPlugin : IPlugin
{
    public const string Id = "devhub.templates";

    public PluginInfo Info { get; } = new(Id, "Templates",
        "Create new projects and files from templates with variables, or turn an existing folder into a template.",
        [ProjectsPlugin.Id]) { Category = "Templates" };

    public void ConfigureServices(IServiceCollection services, PluginContext context)
    {
        services.AddSingleton<DocumentViewModelBase, TemplatesViewModel>();
        services.AddSingleton<DocumentViewModelBase, GenerateViewModel>();
        services.AddSingleton<DocumentViewModelBase, ImportViewModel>();
    }
}
