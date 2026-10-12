using CommunityToolkit.Mvvm.Input;
using DevHub.Actions;
using DevHub.Controls;
using DevHub.Sdk.Core.Plugins;
using DevHub.Services;
using DevHub.ViewModels.ToolWindows;
using Microsoft.Extensions.DependencyInjection;

[assembly: DevHub.Plugins.DevHubPlugin(typeof(DevHub.Plugins.TestPlugin))]

namespace DevHub.Plugins;

/// <summary>The shell's test plugin (tests/DevHub.TestPlugin): its id must equal plugin.json's.</summary>
public sealed class TestPlugin : IPlugin
{
    public PluginInfo Info { get; } = new("devhub.test-plugin", "Test Plugin",
        "Checks the shell: a tool window and an action (tests/DevHub.TestPlugin, never shipped).", [])
    {
        Category = "Tests",
        Order = 9000,
    };

    public void ConfigureServices(IServiceCollection services, PluginContext context)
    {
        services.AddSingleton<TestToolWindowViewModel>();
        services.AddSingleton<ToolWindowViewModelBase>(sp => sp.GetRequiredService<TestToolWindowViewModel>());
        services.AddSingleton<IActionProvider, TestActionProvider>();
    }
}

/// <summary>"Test Plugin: Ping" in Find Action: a status-bar message.</summary>
public sealed class TestActionProvider(IStatusService status) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction("TestPlugin.Ping", "Ping", new RelayCommand(() => status.Show("Test plugin: pong.")))
        {
            Category = "Test Plugin",
            IconKind = IconKind.Info,
        };
    }
}
