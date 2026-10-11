namespace DevHub.Integration;

// Optional links between plugins that don't depend on each other (Projects and Git). They meet
// here, in the Sdk that both build on: each plugin implements what it offers and uses what the
// other offers only if it's registered (inject IEnumerable<T> and take the first, or T? = null).
// Only plain paths cross the line, never a plugin's own types, so neither references the other.

/// <summary>
/// Opens a project folder inside DevHub. Offered by Git (a repository tab); used by Projects
/// for double-click / Enter / "Open in DevHub". Without one, Projects opens the folder in the
/// project's external tool.
/// </summary>
public interface IProjectOpener
{
    /// <summary>Opens the folder; false when it can't (e.g. it's gone).</summary>
    bool Open(string folder);
}

/// <summary>
/// Opens a file in the editor chosen for its project's language (Settings → Open With).
/// Offered by Projects; used by Git's "Open in Editor" (F4). Without one, Git opens the file
/// with the program Windows associates with it.
/// </summary>
public interface IProjectFileEditor
{
    /// <param name="projectFolder">The project (repository) the file belongs to: decides the language and so the editor.</param>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="line">1-based line to jump to, or null.</param>
    /// <remarks>Reports success or problems in the status bar itself (callers are buttons).</remarks>
    void OpenFile(string projectFolder, string filePath, int? line);
}

/// <summary>
/// The folders the user keeps projects in. Offered by Projects; used by Git's Clone (default
/// target folder, and a rescan afterwards so the clone shows up in Projects).
/// </summary>
public interface IProjectFolders
{
    IReadOnlyList<string> Folders { get; }

    /// <summary>Looks for projects in the folders again.</summary>
    Task RescanAsync();
}
