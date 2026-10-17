using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Services;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Actions.Providers;

/// <summary>"Restart DevHub": loads a changed plugin choice (plugins are only loaded at startup).</summary>
public sealed class RestartActionProvider(IAppRestarter restarter) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.Restart, L.T("Restart DevHub"), new RelayCommand(restarter.Restart))
        {
            Category = "File",
            IconKind = IconKind.Refresh,
            Menu = "File",
            MenuOrder = 950,
            Description = L.T("Start DevHub again, e.g. to apply changes in Settings → Plugins. Open tabs come back."),
        };
    }
}
