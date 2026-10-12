namespace DevHub.Integration;

// Links between the Git plugin and plugins that build on what happens in a repository (Board.Git, CI): offered by
// one side, used by the other only when it's registered (IEnumerable<T>, or T? = null). Plain data only (paths,
// commit SHAs, branch names), never a plugin's own types. docs/PLUGINS.md, "Optional link between independent plugins".

/// <summary>What happened in a repository: <see cref="IRepositoryEvents"/>.</summary>
/// <param name="RepositoryFolder">The repository's top folder.</param>
/// <param name="Branch">The current branch after the change (null: detached HEAD).</param>
/// <param name="CommitSha">The commit concerned: the new HEAD, the commit just made, or what was pushed.</param>
public sealed record RepositoryEventArgs(string RepositoryFolder, string? Branch, string? CommitSha);

/// <summary>A push: which local branch went to which remote branch.</summary>
public sealed record PushEventArgs(string RepositoryFolder, string Branch, string Remote, string RemoteBranch, string CommitSha);

/// <summary>
/// Branch and commit events of the repositories DevHub has open. Offered by Git; used by plugins that react to them
/// (Board.Git moves an issue when its branch is pushed, CI refreshes the runs of the pushed commit). Raised on the
/// UI thread, after the change is done; a handler that throws is the handler's problem (the next one still runs).
/// Unsubscribe when you're done (the Git plugin lives as long as DevHub).
/// </summary>
public interface IRepositoryEvents
{
    /// <summary>HEAD moved to another branch (checkout, a new branch, a rebase that ended…).</summary>
    event EventHandler<RepositoryEventArgs>? BranchChanged;

    /// <summary>A commit was made from DevHub (commit, amend, merge commit).</summary>
    event EventHandler<RepositoryEventArgs>? Committed;

    /// <summary>A push from DevHub succeeded.</summary>
    event EventHandler<PushEventArgs>? Pushed;

    /// <summary>A fetch or pull from DevHub brought in new commits (the remote branches may have moved).</summary>
    event EventHandler<RepositoryEventArgs>? Fetched;
}

/// <summary>What the Git plugin's commit box is about to commit, for a <see cref="ICommitMessageSuggester"/>.</summary>
/// <param name="StagedFiles">Repository-relative paths ('/' separators) of the staged changes.</param>
public sealed record CommitContext(string RepositoryFolder, string? Branch, IReadOnlyList<string> StagedFiles);

/// <summary>
/// Suggests a commit message (or a start for one): Board.Git suggests "DEV-12: " for a branch named
/// feature/DEV-12-…. Offered by any plugin; used by Git's commit box, which asks every suggester when the message is
/// empty and offers the first answer (it's never put in without the user seeing it). Answer fast (no network) or null.
/// </summary>
public interface ICommitMessageSuggester
{
    Task<string?> SuggestAsync(CommitContext context, CancellationToken ct);
}

/// <summary>How a decoration looks: the view maps it to the design tokens (Brush.Status.*).</summary>
public enum DecorationTone
{
    Neutral,
    Running,
    Success,
    Warning,
    Error,
}

/// <summary>
/// A small mark next to a commit or a branch in Git's views: an icon (an IconKind name), a tone, a tooltip, and
/// optionally a link to open on click (http(s) only).
/// </summary>
public sealed record Decoration(string IconKind, DecorationTone Tone, string ToolTip, string? Url = null);

/// <summary>Which decorations changed: their repository, and (when known) the commits or branches.</summary>
public sealed record DecorationsChangedEventArgs(string RepositoryFolder, IReadOnlyList<string>? CommitShas = null, IReadOnlyList<string>? Branches = null);

/// <summary>
/// Marks for commits and branches (CI shows ✓ / ✗ / ● per commit and per branch's latest run; Board could show issue
/// keys). Offered by any plugin; used by Git's commit list, commit details and branch tree, which ask for what's on
/// screen and ask again when <see cref="Changed"/> says so. Answers come from what the plugin already knows (a cache):
/// fetch in the background and raise <see cref="Changed"/>, don't make Git wait for the network.
/// </summary>
public interface IGitDecorator
{
    /// <summary>Decorations of these commits (full SHAs); commits without any are left out.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<Decoration>> DecorateCommits(string repositoryFolder, IReadOnlyList<string> commitShas);

    /// <summary>Decorations of these branches (short names, local "main" or remote "origin/main"); branches without any are left out.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<Decoration>> DecorateBranches(string repositoryFolder, IReadOnlyList<string> branches);

    /// <summary>Raised (on any thread) when decorations changed; Git asks again for what it shows.</summary>
    event EventHandler<DecorationsChangedEventArgs>? Changed;
}
