namespace DevHub.Integration;

/// <summary>A diagram or mood board file, as other plugins see it (plain data).</summary>
/// <param name="Path">The <c>*.devhub-diagram.json</c> file.</param>
/// <param name="Name">"Login" (the file name without its ending).</param>
/// <param name="KindName">"Use case diagram", "Mood board" (translated).</param>
/// <param name="IconKind">An <c>IconKind</c> name for lists.</param>
public sealed record DiagramFileInfo(string Path, string Name, string KindName, string IconKind);

/// <summary>
/// A project's diagrams (the Diagrams plugin, when it's on): the Projects tool window lists them under each project,
/// a board shows its folder's diagrams, an issue the diagrams that mention it. A diagram belongs to a project when it
/// lies in its folder or its file links to it. Inject as <c>IProjectDiagrams? diagrams = null</c> and show nothing
/// without it. Paths only: neither side knows the other's types.
/// </summary>
public interface IProjectDiagrams
{
    /// <summary>The diagrams of a project folder (in it, or linked to it), by name. Reads files: call off the UI thread.</summary>
    IReadOnlyList<DiagramFileInfo> DiagramsOf(string projectFolder);

    /// <summary>The diagrams under a folder whose shapes are linked to an issue key ("SHOP-12").</summary>
    IReadOnlyList<DiagramFileInfo> Mentioning(string folder, string issueKey);

    /// <summary>Opens a diagram in its tab.</summary>
    void Open(string path);

    /// <summary>Asks for a kind and a name, creates the diagram in the project's <c>docs/diagrams</c> and opens it; its path, or null.</summary>
    Task<string?> CreateAsync(string projectFolder);

    /// <summary>A diagram was created, deleted, renamed or (re)linked: lists showing diagrams refresh.</summary>
    event EventHandler? Changed;
}

/// <summary>An issue a diagram shape can be linked to (plain data).</summary>
/// <param name="Key">"SHOP-12".</param>
/// <param name="StatusCategory">"todo", "inprogress" or "done", for the badge colour on the shape.</param>
public sealed record IssueLinkInfo(string Key, string Summary, string Status, string StatusCategory);

/// <summary>
/// Issues for diagrams to link to (the Board plugin, when it's on): a use case or an action shows its issue's status
/// and opens it; "Create Story from Use Case" makes one. Searches the boards DevHub knows (open ones first). Inject as
/// <c>IIssueLinks? issues = null</c>; without it diagrams keep the key as plain text.
/// </summary>
public interface IIssueLinks
{
    /// <summary>Issues whose key or summary match (empty text: recent ones), at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<IssueLinkInfo>> SearchAsync(string text, int max = 20);

    /// <summary>The issue with that key, or null (deleted, or on a board DevHub doesn't know).</summary>
    Task<IssueLinkInfo?> FindAsync(string key);

    /// <summary>Opens the issue on its board.</summary>
    void Open(string key);

    /// <summary>Creates a story (the board's default issue type) and returns it, or null when cancelled.</summary>
    Task<IssueLinkInfo?> CreateStoryAsync(string summary, string description);

    /// <summary>Issues changed (status, summary): linked shapes refresh their badges.</summary>
    event EventHandler? Changed;
}
