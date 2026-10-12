using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Services;

namespace DevHub.Actions.Providers;

/// <summary>Help → Check for Updates: asks GitHub Releases for a newer DevHub and says what it found.</summary>
public sealed class UpdateActionProvider(UpdateService updates, IStatusService status) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.CheckForUpdates, "Check for Updates", new AsyncRelayCommand(async () => status.Show(await updates.CheckAsync())))
        {
            Category = "Help",
            IconKind = IconKind.Update,
            Menu = "Help",
            MenuOrder = 100,
            Description = "Look for a newer DevHub on GitHub Releases (Settings → Updates has more).",
        };
    }
}
