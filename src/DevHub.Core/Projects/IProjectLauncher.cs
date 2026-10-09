using DevHub.Core.Repositories;

namespace DevHub.Core.Projects;

/// <summary>
/// Opens a repository with the appropriate tool, or reveals it in the OS file manager.
/// </summary>
public interface IProjectLauncher
{
    /// <summary>
    /// Opens the repo with the "right tool": a solution/project file via its OS association
    /// (e.g. a .sln in the user's default IDE), otherwise the folder in VS Code, otherwise the
    /// file manager. Throws if the chosen target can't be launched.
    /// </summary>
    void Open(GitRepository repo);

    /// <summary>Reveals the repo folder in the OS file manager (always available).</summary>
    void RevealInFileManager(GitRepository repo);
}
