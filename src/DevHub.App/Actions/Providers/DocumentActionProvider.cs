using CommunityToolkit.Mvvm.Input;
using DevHub.ViewModels.Documents;

namespace DevHub.Actions.Providers;

/// <summary>
/// "Open X" for every single document (label, shortcut, menu and Welcome place come from the
/// document's <see cref="DocumentViewModelBase.Navigation"/>), plus tab handling: Close Tab
/// (Ctrl+F4), Next/Previous Tab (Alt+Right / Alt+Left).
/// </summary>
public sealed class DocumentActionProvider(DocumentManager documents) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        foreach (var document in documents.Available)
        {
            var captured = document;
            var navigation = document.Navigation;

            yield return new AppAction(ActionIds.NavigatePrefix + document.Id, navigation.Text ?? document.Title,
                new RelayCommand(() => documents.Show(captured)))
            {
                Category = "Navigate",
                IconKind = document.Icon,
                Gesture = navigation.Gesture,
                Menu = navigation.Menu,
                MenuOrder = navigation.MenuOrder,
                WelcomeOrder = navigation.WelcomeOrder,
            };
        }

        yield return new AppAction(ActionIds.NextTab, "Next Tab", new RelayCommand(() => documents.Cycle(+1)))
        {
            Category = "Editor",
            Gesture = "Alt+Right",
            Menu = "View",
            MenuOrder = 200,
        };
        yield return new AppAction(ActionIds.PreviousTab, "Previous Tab", new RelayCommand(() => documents.Cycle(-1)))
        {
            Category = "Editor",
            Gesture = "Alt+Left",
            Menu = "View",
            MenuOrder = 201,
        };
        yield return new AppAction(ActionIds.CloseTab, "Close Tab", documents.CloseActiveCommand)
        {
            Category = "Editor",
            IconKind = Controls.IconKind.Close,
            Gesture = "Ctrl+F4",
            Menu = "View",
            MenuOrder = 202,
        };
    }
}
