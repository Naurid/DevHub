using CommunityToolkit.Mvvm.Input;
using DevHub.ViewModels.Documents;

namespace DevHub.Actions.Providers;

/// <summary>
/// "Open X" for every document (New Project = Ctrl+N, like Rider), plus
/// tab handling: Close Tab (Ctrl+F4), Next/Previous Tab (Alt+Right / Alt+Left).
/// </summary>
public sealed class DocumentActionProvider(DocumentManager documents) : IActionProvider
{
    /// <summary>Shortcuts and nicer menu labels for specific documents (by document Id).</summary>
    private static readonly Dictionary<string, (string Text, string? Gesture)> Overrides = new()
    {
        ["Generate"] = ("New Project…", "Ctrl+N"),
        ["Import"] = ("Import Folder…", null),
    };

    public IEnumerable<AppAction> GetActions()
    {
        foreach (var document in documents.Available)
        {
            var captured = document;
            var (text, gesture) = Overrides.TryGetValue(document.Id, out var o) ? o : (document.Title, null);

            yield return new AppAction(ActionIds.NavigatePrefix + document.Id, text,
                new RelayCommand(() => documents.Show(captured)))
            {
                Category = "Navigate",
                IconKind = document.Icon,
                Gesture = gesture,
            };
        }

        yield return new AppAction(ActionIds.CloseTab, "Close Tab", documents.CloseActiveCommand)
        {
            Category = "Editor",
            IconKind = Controls.IconKind.Close,
            Gesture = "Ctrl+F4",
        };
        yield return new AppAction(ActionIds.NextTab, "Next Tab", new RelayCommand(() => documents.Cycle(+1)))
        {
            Category = "Editor",
            Gesture = "Alt+Right",
        };
        yield return new AppAction(ActionIds.PreviousTab, "Previous Tab", new RelayCommand(() => documents.Cycle(-1)))
        {
            Category = "Editor",
            Gesture = "Alt+Left",
        };
    }
}
