using DevHub.Controls;
using DevHub.ViewModels.Pages;

namespace DevHub.Actions.Providers;

/// <summary>
/// Repository actions. They wrap the page's existing commands, so CanExecute (e.g. "Rescan"
/// is disabled while scanning or with no folders) is shared with the page's own buttons.
/// </summary>
public sealed class RepositoryActionProvider(RepositoriesViewModel repositories) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.AddScanFolder, "Add Folder…", repositories.AddFolderCommand)
        {
            Category = "Repositories",
            IconKind = IconKind.Add,
            Description = "Choose a folder to scan for git repositories.",
        };

        yield return new AppAction(ActionIds.RescanRepositories, "Rescan", repositories.ScanCommand)
        {
            Category = "Repositories",
            IconKind = IconKind.Refresh,
            Gesture = "F5",
            Description = "Scan the chosen folders for git repositories again.",
        };
    }
}
