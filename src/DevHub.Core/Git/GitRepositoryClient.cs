namespace DevHub.Core.Git;

/// <summary>
/// Git operations on one repository (one client per repository tab). Every method runs git
/// through <see cref="GitRunner"/> in the repository's folder. G0 has the read-only basics;
/// status, diff, staging, branches… are added milestone by milestone (docs/GIT_CLIENT_PLAN.md).
/// </summary>
public sealed class GitRepositoryClient(GitRunner runner, string repositoryPath)
{
    public string RepositoryPath { get; } = repositoryPath;

    /// <summary>True when the folder is inside a git work tree.</summary>
    public async Task<bool> IsRepositoryAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(RepositoryPath))
            return false;

        var result = await runner.RunAsync(RepositoryPath, ["rev-parse", "--is-inside-work-tree"],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        return result.Succeeded && result.StandardOutput.Trim() == "true";
    }

    /// <summary>The repository's root folder (where .git is), in OS path form.</summary>
    public async Task<string> GetTopLevelAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath, ["rev-parse", "--show-toplevel"],
            new GitRunOptions { ReadOnly = true }, ct);
        return Path.GetFullPath(result.StandardOutput.Trim());
    }

    /// <summary>
    /// The checked-out branch ("main"), or for a detached HEAD the short commit id ("1a2b3c4").
    /// Null for a new repository with no commits on a detached HEAD.
    /// </summary>
    public async Task<string?> GetBranchAsync(CancellationToken ct = default)
    {
        // symbolic-ref -q: exit 1 (no error text) when HEAD is detached.
        var branch = await runner.RunAsync(RepositoryPath, ["symbolic-ref", "--short", "-q", "HEAD"],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        if (branch.Succeeded)
            return branch.StandardOutput.Trim();

        var commit = await runner.RunAsync(RepositoryPath, ["rev-parse", "--short", "HEAD"],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        return commit.Succeeded ? commit.StandardOutput.Trim() : null;
    }
}
