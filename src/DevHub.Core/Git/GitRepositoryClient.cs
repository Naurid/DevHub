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
    /// Branch, upstream, ahead/behind and every changed file (staged, unstaged, untracked,
    /// conflicted). Untracked folders are listed as one entry ending in "/" (git's "normal"
    /// mode), so huge untracked trees (e.g. a Unity Library folder) stay cheap.
    /// </summary>
    public async Task<RepoStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath,
            ["status", "--porcelain=v2", "--branch", "-z", "--untracked-files=normal"],
            new GitRunOptions { ReadOnly = true }, ct);
        return StatusParser.Parse(result.StandardOutput);
    }

    /// <summary>
    /// Merge / rebase / cherry-pick / revert in progress, detected from the marker files git
    /// leaves in the .git folder (status --porcelain doesn't report it).
    /// </summary>
    public async Task<RepositoryOperation> GetOperationAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath, ["rev-parse", "--absolute-git-dir"],
            new GitRunOptions { ReadOnly = true }, ct);
        var gitDir = result.StandardOutput.Trim();

        if (Directory.Exists(Path.Combine(gitDir, "rebase-merge")) || Directory.Exists(Path.Combine(gitDir, "rebase-apply")))
            return RepositoryOperation.Rebasing;
        if (File.Exists(Path.Combine(gitDir, "MERGE_HEAD")))
            return RepositoryOperation.Merging;
        if (File.Exists(Path.Combine(gitDir, "CHERRY_PICK_HEAD")))
            return RepositoryOperation.CherryPicking;
        if (File.Exists(Path.Combine(gitDir, "REVERT_HEAD")))
            return RepositoryOperation.Reverting;
        return RepositoryOperation.None;
    }

    /// <summary>
    /// The diff of one changed file: the staged side (index vs HEAD) when <paramref name="staged"/>,
    /// else the unstaged side (working tree vs index). Untracked files are shown as all-new;
    /// conflicted files as git's combined diff. Never null: a file with nothing to show gives a
    /// <see cref="FileDiff"/> without hunks.
    /// </summary>
    public async Task<FileDiff> GetDiffAsync(FileChange change, bool staged, CancellationToken ct = default,
        int maxLines = DiffParser.DefaultMaxLines)
    {
        if (change.Kind == FileChangeKind.Untracked)
        {
            if (change.Path.EndsWith('/'))
                return new FileDiff { NewPath = change.Path, IsNewFile = true, IsDirectory = true };

            // --no-index compares two paths outside git's index; /dev/null means "empty file"
            // (git handles that name itself, on Windows too). Exit code 1 = "there are differences".
            string[] noIndex = ["diff", "--no-index", .. CommonDiffArgs, "--", "/dev/null", change.Path];
            var untracked = await runner.RunAsync(RepositoryPath, noIndex,
                new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
            if (untracked.ExitCode > 1)
                throw new GitException(noIndex, untracked.ExitCode, untracked.StandardError);
            return First(untracked.StandardOutput, change.Path, isNew: true);
        }

        List<string> args = ["diff", .. CommonDiffArgs];
        if (staged)
            args.AddRange(["--cached", "-M"]);
        args.Add("--");
        args.Add(change.Path);
        if (staged && change.OriginalPath is { } from)
            args.Add(from); // a rename needs both paths, or git shows an add and a delete

        var result = await runner.RunAsync(RepositoryPath, args, new GitRunOptions { ReadOnly = true }, ct);
        return First(result.StandardOutput, change.Path, isNew: false);

        FileDiff First(string output, string path, bool isNew) =>
            DiffParser.Parse(output, maxLines).FirstOrDefault()
            ?? new FileDiff { OldPath = isNew ? null : path, NewPath = path, IsNewFile = isNew };
    }

    // ---- Changing the repository (G3). Only ever called from a user action. -------------------

    /// <summary>
    /// Stages files (<c>git add -A</c>: new, modified and deleted alike). For a rename, pass
    /// both paths. Paths go through stdin, so any number of files fits.
    /// </summary>
    public Task StageAsync(IEnumerable<string> paths, CancellationToken ct = default) =>
        RunWithPathsAsync(["add", "-A"], paths, ct);

    /// <summary>
    /// Unstages files: the index goes back to HEAD for them, the working tree is untouched
    /// (<c>git reset</c>, which also works before the first commit). For a rename, pass both paths.
    /// </summary>
    public Task UnstageAsync(IEnumerable<string> paths, CancellationToken ct = default) =>
        RunWithPathsAsync(["reset", "-q"], paths, ct);

    /// <summary>Stages every change (<c>git add -A</c>).</summary>
    public async Task StageAllAsync(CancellationToken ct = default) =>
        await runner.RunAsync(RepositoryPath, ["add", "-A"], ct: ct);

    /// <summary>Unstages everything; working tree untouched (<c>git reset -q</c>).</summary>
    public async Task UnstageAllAsync(CancellationToken ct = default) =>
        await runner.RunAsync(RepositoryPath, ["reset", "-q"], ct: ct);

    /// <summary>
    /// Throws away unstaged changes: tracked files go back to their staged (or committed)
    /// content, untracked files and folders are deleted. Staged changes are kept. Destructive:
    /// the caller must have asked the user. Conflicted files are refused (G7 resolves those).
    /// </summary>
    public async Task DiscardAsync(IReadOnlyCollection<FileChange> changes, CancellationToken ct = default)
    {
        if (changes.Any(c => c.IsConflicted))
            throw new InvalidOperationException("Conflicted files can't be discarded here; resolve the conflict first.");

        var tracked = changes.Where(c => c.Kind != FileChangeKind.Untracked).Select(c => c.Path).ToList();
        var untracked = changes.Where(c => c.Kind == FileChangeKind.Untracked).Select(c => c.Path).ToList();

        if (tracked.Count > 0)
            await RunWithPathsAsync(["restore", "--worktree"], tracked, ct);
        if (untracked.Count > 0)
        {
            // clean has no --pathspec-from-file; "--" stops a file named like an option being read as one.
            // -d: also whole untracked folders ("newdir/"); without -x ignored files stay.
            await runner.RunAsync(RepositoryPath, ["clean", "-f", "-d", "-q", "--", .. untracked], ct: ct);
        }
    }

    /// <summary>
    /// Commits what's staged with <paramref name="message"/> (sent on stdin, so any length and
    /// any characters are safe). <paramref name="amend"/> replaces the last commit instead.
    /// Hooks run as usual; a failing hook throws <see cref="GitException"/> with its output.
    /// Returns the new commit id.
    /// </summary>
    public async Task<string> CommitAsync(string message, bool amend = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A commit needs a message.", nameof(message));

        List<string> args = ["commit", "-F", "-"];
        if (amend)
            args.Add("--amend");
        // A Windows text box may give "\r\n"; git messages use "\n".
        var normalized = message.Replace("\r\n", "\n", StringComparison.Ordinal);
        await runner.RunAsync(RepositoryPath, args, new GitRunOptions { StandardInput = normalized }, ct);

        var head = await runner.RunAsync(RepositoryPath, ["rev-parse", "HEAD"], new GitRunOptions { ReadOnly = true }, ct);
        return head.StandardOutput.Trim();
    }

    /// <summary>The last commit's full message (to edit when amending), or null before the first commit.</summary>
    public async Task<string?> GetLastCommitMessageAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath, ["log", "-1", "--format=%B"],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        return result.Succeeded ? result.StandardOutput.TrimEnd() : null;
    }

    /// <summary>
    /// Runs a command on a list of paths given NUL-separated on stdin
    /// (<c>--pathspec-from-file=- --pathspec-file-nul</c>): no command-line length limit and no
    /// quoting problems. <c>--literal-pathspecs</c> makes git take each path as-is, so a file
    /// named "*.txt" or ":x" means exactly that file, not a pattern.
    /// </summary>
    private async Task RunWithPathsAsync(IReadOnlyList<string> command, IEnumerable<string> paths, CancellationToken ct)
    {
        var list = paths.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        if (list.Count == 0)
            return;

        await runner.RunAsync(RepositoryPath,
            ["--literal-pathspecs", .. command, "--pathspec-from-file=-", "--pathspec-file-nul"],
            new GitRunOptions { StandardInput = string.Join('\0', list) }, ct);
    }

    /// <summary>
    /// Flags that pin the diff format, whatever the user's git config says: no color, no external
    /// diff tool, a/ b/ prefixes (diff.noprefix / mnemonicPrefix would change them).
    /// </summary>
    private static readonly string[] CommonDiffArgs =
        ["--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/"];

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
