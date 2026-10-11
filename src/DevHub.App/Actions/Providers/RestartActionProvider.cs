using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Services;

namespace DevHub.Actions.Providers;

/// <summary>"Restart DevHub": loads a changed plugin choice (plugins are only loaded at startup).</summary>
public sealed class RestartActionProvider(IAppRestarter restarter) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.Restart, "Restart DevHub", new RelayCommand(restarter.Restart))
        {
            Category = "File",
            IconKind = IconKind.Refresh,
            Menu = "File",
            MenuOrder = 950,
            Description = "Start DevHub again, e.g. to apply changes in Settings → Plugins. Open tabs come back.",
        };
    }
}
