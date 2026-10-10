using DevHub.Controls;
using DevHub.ViewModels.ToolWindows;

namespace DevHub.Actions.Providers;

/// <summary>
/// Projects tool window actions (shown as "Projects"; ids keep the old "Repositories." prefix so saved
/// shortcuts stay valid). They wrap the tool window's existing commands, so CanExecute (e.g. "Rescan"
/// is disabled while scanning or with no folders) is shared with the tool window's own buttons.
/// </summary>
public sealed class RepositoryActionProvider(RepositoriesViewModel repositories) : IActionProvider
{
    public IEnumerable<AppAction> GetActions()
    {
        yield return new AppAction(ActionIds.AddScanFolder, "Add Folder…", repositories.AddFolderCommand)
        {
            Category = "Projects",
            IconKind = IconKind.Add,
            Description = "Choose a folder to scan for projects (git repositories).",
        };

        yield return new AppAction(ActionIds.RescanRepositories, "Rescan", repositories.ScanCommand)
        {
            Category = "Projects",
            IconKind = IconKind.Refresh,
            Gesture = "F5",
            Description = "Scan the project folders for git repositories again.",
        };
    }
}
