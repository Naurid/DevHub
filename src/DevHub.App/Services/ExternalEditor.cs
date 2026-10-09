using DevHub.Core.Projects;
using DevHub.Core.Repositories;

namespace DevHub.Services;

/// <summary>
/// Opens a repository file in the external editor tied to the repository's language
/// (Settings → Open With), e.g. the diff pane's "Open in Editor" (F4). Problems go to the
/// status bar instead of throwing, since callers are buttons.
/// </summary>
public sealed class ExternalEditor(IProjectLauncher launcher, IProjectLanguageDetector detector, IStatusService status)
{
    /// <param name="repositoryPath">Repository root folder.</param>
    /// <param name="gitPath">The file as git names it (relative, forward slashes).</param>
    /// <param name="line">1-based line to jump to, or null.</param>
    public void Open(string repositoryPath, string gitPath, int? line)
    {
        var fullPath = Path.GetFullPath(Path.Combine(repositoryPath, gitPath.Replace('/', Path.DirectorySeparatorChar)));
        var name = Path.GetFileName(repositoryPath.TrimEnd('\\', '/'));
        var repo = new GitRepository(name, repositoryPath, detector.Detect(repositoryPath));
        try
        {
            launcher.OpenFile(repo, fullPath, line);
            status.Show($"Opened {gitPath}.");
        }
        catch (Exception ex)
        {
            status.Show($"Couldn't open {gitPath}: {ex.Message}");
        }
    }
}
