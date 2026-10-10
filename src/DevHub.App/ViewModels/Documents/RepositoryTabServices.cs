using DevHub.Services;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// The app services a repository tab (and its diff tabs) use, in one DI-registered bundle so
/// the factories pass one thing instead of a growing parameter list.
/// </summary>
/// <param name="Editor">Opens files in the external editor for the repository's language.</param>
/// <param name="Dialogs">Confirmations (discard).</param>
/// <param name="Status">Status bar messages.</param>
/// <param name="GitHub">The GitHub account (Publish to GitHub).</param>
/// <param name="Terminal">Opens a terminal in the repository (Open Terminal).</param>
public sealed record RepositoryTabServices(ExternalEditor Editor, IDialogService Dialogs, IStatusService Status, GitHubSession GitHub,
    TerminalLauncher Terminal);
