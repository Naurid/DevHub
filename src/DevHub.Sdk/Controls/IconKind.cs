namespace DevHub.Controls;

/// <summary>
/// Names of the vendored icons in <c>Assets/Icons/</c> (each has a light and a
/// <c>_dark</c> file). Plain strings, so ViewModels can use them without referencing
/// any Avalonia types. To add an icon: copy both SVGs from intellij-community
/// <c>platform/icons/src/expui/</c>, add a constant here, and list it in Assets/Icons/NOTICE.md.
/// </summary>
public static class IconKind
{
    // General actions
    public const string Add = "add";
    public const string Close = "close";
    public const string Copy = "copy";
    public const string Delete = "delete";
    public const string Edit = "edit";
    public const string ExternalTools = "externalTools";
    public const string Import = "import";
    public const string MoreVertical = "moreVertical";
    public const string Refresh = "refresh";
    public const string Search = "search";
    public const string Settings = "settings";

    // Navigation
    public const string ChevronDown = "chevronDown";
    public const string ChevronRight = "chevronRight";
    public const string Menu = "menu";

    // Nodes / tool windows
    public const string Folder = "folder";
    public const string Template = "template";
    public const string Palette = "palette";
    public const string Vcs = "vcs";

    // Version control
    public const string Commit = "commit";
    public const string Fetch = "fetch";
    public const string Merge = "merge";
    public const string Push = "push";
    public const string Update = "update";

    // Run
    public const string Run = "run";
    public const string Terminal = "terminal";
    public const string Stop = "stop";

    // Status
    public const string Error = "error";
    public const string Info = "info";
    public const string Success = "success";
    public const string Warning = "warning";

    /// <summary>Every icon, for the UI Gallery.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Add, Close, Copy, Delete, Edit, ExternalTools, Import, MoreVertical, Refresh, Search, Settings,
        ChevronDown, ChevronRight, Menu,
        Folder, Template, Palette, Vcs,
        Commit, Fetch, Merge, Push, Update,
        Run, Stop,
        Error, Info, Success, Warning
    ];
}
