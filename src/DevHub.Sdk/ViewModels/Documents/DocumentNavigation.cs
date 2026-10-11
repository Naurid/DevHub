namespace DevHub.ViewModels.Documents;

/// <summary>
/// How a document's "open" action looks and where it appears (see
/// <see cref="DocumentViewModelBase.Navigation"/>). Defaults: the tab title, no shortcut, only in
/// Find Action. The orders follow <c>AppAction</c>'s rule (a new hundred starts a new group).
/// </summary>
public sealed record DocumentNavigation
{
    /// <summary>Menu / Welcome label, e.g. "New Project…"; null = the tab title.</summary>
    public string? Text { get; init; }

    /// <summary>Shortcut, e.g. "Ctrl+N".</summary>
    public string? Gesture { get; init; }

    /// <summary>Main-menu group ("File", "View", …); null = not in the main menu.</summary>
    public string? Menu { get; init; }

    public int MenuOrder { get; init; }

    /// <summary>Position in the Welcome view; null = not there.</summary>
    public int? WelcomeOrder { get; init; }
}
