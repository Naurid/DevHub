using CommunityToolkit.Mvvm.Input;
using DevHub.Controls;
using DevHub.Services;
using DevHub.ViewModels.Dialogs.Settings;

namespace DevHub.Actions.Providers;

/// <summary>"Settings…" (Ctrl+Alt+S, like Rider): opens the Settings dialog.</summary>
/// <remarks>
/// Takes a factory (<c>Func&lt;SettingsDialogViewModel&gt;</c>) rather than one dialog instance:
/// every opening needs a fresh dialog that starts from the saved values.
/// </remarks>
public sealed class SettingsActionProvider(IDialogService dialogs, Func<SettingsDialogViewModel> createDialog)
    : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.OpenSettings, "Settings…",
            new AsyncRelayCommand(() => dialogs.ShowDialogAsync(createDialog())))
        {
            Category = "File",
            IconKind = IconKind.Settings,
            Gesture = "Ctrl+Alt+S",
            Menu = "File",
            MenuOrder = 900,
            ToolbarOrder = 201,
            WelcomeOrder = 900,
            Description = "Appearance, plugins and the preferences of each plugin.",
        };
    }
}
