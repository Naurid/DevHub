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

    // ---- Branches (G4) -------------------------------------------------------------------------

    /// <summary>Local and remote-tracking branches (not "origin/HEAD"), sorted by git by ref name.</summary>
    public async Task<IReadOnlyList<GitBranch>> GetBranchesAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath,
            ["for-each-ref", "--format=" + BranchParser.Format, "refs/heads", "refs/remotes"],
            new GitRunOptions { ReadOnly = true }, ct);
        return BranchParser.Parse(result.StandardOutput);
    }

    /// <summary>
    /// Switches to <paramref name="branch"/>. A remote branch ("origin/feature") gets a local
    /// tracking branch of the same name, or switches to that local branch if it exists. Local
    /// changes that don't clash come along (git's normal behaviour); if they would be
    /// overwritten nothing happens and <see cref="CheckoutResult.LocalChangesInTheWay"/> is returned.
    /// </summary>
    public async Task<CheckoutResult> CheckoutAsync(GitBranch branch, CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath, await SwitchArgsAsync(branch, ct),
            new GitRunOptions { AllowFailure = true }, ct);
        if (result.Succeeded)
            return CheckoutResult.Done;
        if (IsLocalChangesInTheWay(result.StandardError))
            return CheckoutResult.LocalChangesInTheWay;
        throw new GitException(["switch", branch.ShortName], result.ExitCode, result.StandardError);
    }

    /// <summary>
    /// JetBrains' Smart Checkout: stash the local changes (untracked files too), switch, and
    /// re-apply them. If re-applying conflicts, git keeps the stash and leaves the conflicts in
    /// the working tree, so nothing is lost.
    /// </summary>
    public async Task<SmartCheckoutResult> SmartCheckoutAsync(GitBranch branch, CancellationToken ct = default)
    {
        var stashBefore = await StashTopAsync(ct);
        await runner.RunAsync(RepositoryPath,
            ["stash", "push", "--include-untracked", "-m", $"DevHub: local changes before checkout of {branch.ShortName}"], ct: ct);

        // "Nothing to stash" also exits 0. Popping then would apply an older, unrelated stash,
        // so only pop when this call actually made one.
        if (await StashTopAsync(ct) == stashBefore)
        {
            await runner.RunAsync(RepositoryPath, await SwitchArgsAsync(branch, ct), ct: ct);
            return SmartCheckoutResult.Done;
        }

        try
        {
            await runner.RunAsync(RepositoryPath, await SwitchArgsAsync(branch, ct), ct: ct);
        }
        catch
        {
            // Switching failed after all: put the changes back where they were.
            await runner.RunAsync(RepositoryPath, ["stash", "pop"], new GitRunOptions { AllowFailure = true }, CancellationToken.None);
            throw;
        }

        var pop = await runner.RunAsync(RepositoryPath, ["stash", "pop"], new GitRunOptions { AllowFailure = true }, ct);
        return pop.Succeeded ? SmartCheckoutResult.Done : SmartCheckoutResult.ReapplyConflicted;
    }

    /// <summary>The newest stash's commit id, or null when there are no stashes.</summary>
    private async Task<string?> StashTopAsync(CancellationToken ct)
    {
        var result = await runner.RunAsync(RepositoryPath, ["rev-parse", "--verify", "--quiet", "refs/stash"],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    private async Task<IReadOnlyList<string>> SwitchArgsAsync(GitBranch branch, CancellationToken ct)
    {
        if (!branch.IsRemote)
            return ["switch", branch.Name];

        // Remote branch: use the local branch of that name if there is one, else create it tracking the remote.
        var local = await runner.RunAsync(RepositoryPath, ["rev-parse", "--verify", "--quiet", "refs/heads/" + branch.Name],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        return local.Succeeded
            ? ["switch", branch.Name]
            : ["switch", "-c", branch.Name, "--track", branch.ShortName];
    }

    private static bool IsLocalChangesInTheWay(string stderr) =>
        stderr.Contains("would be overwritten by checkout", StringComparison.Ordinal)
        || stderr.Contains("would be overwritten by switch", StringComparison.Ordinal)
        || stderr.Contains("untracked working tree files would be", StringComparison.Ordinal);

    /// <summary>
    /// Creates a branch at <paramref name="startPoint"/> (a branch, commit…; null = HEAD) and,
    /// with <paramref name="checkout"/>, switches to it (local changes come along).
    /// </summary>
    public async Task CreateBranchAsync(string name, string? startPoint = null, bool checkout = true, CancellationToken ct = default)
    {
        List<string> args = checkout ? ["switch", "-c", name] : ["branch", name];
        if (startPoint is not null)
        {
            args.Add(startPoint);
            // Starting from a remote branch: don't make the new branch track it (it's a new branch).
            args.Add("--no-track");
        }
        await runner.RunAsync(RepositoryPath, args, ct: ct);
    }

    /// <summary>Renames a local branch (<c>git branch -m</c>); works for the current branch too.</summary>
    public async Task RenameBranchAsync(string oldName, string newName, CancellationToken ct = default) =>
        await runner.RunAsync(RepositoryPath, ["branch", "-m", "--", oldName, newName], ct: ct);

    /// <summary>
    /// Deletes a local branch. Without <paramref name="force"/> git refuses a branch whose
    /// commits aren't merged anywhere: then false is returned and nothing is deleted, so the
    /// caller can ask ("those commits will be lost") and call again with force.
    /// </summary>
    public async Task<bool> DeleteBranchAsync(string name, bool force = false, CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath, ["branch", force ? "-D" : "-d", "--", name],
            new GitRunOptions { AllowFailure = true }, ct);
        if (result.Succeeded)
            return true;
        if (!force && result.StandardError.Contains("not fully merged", StringComparison.Ordinal))
            return false;
        throw new GitException(["branch", force ? "-D" : "-d", name], result.ExitCode, result.StandardError);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is a valid new branch name (git's own rules:
    /// <c>check-ref-format --branch</c>). Doesn't check whether it already exists.
    /// </summary>
    public async Task<bool> IsValidBranchNameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-'))
            return false;
        var result = await runner.RunAsync(RepositoryPath, ["check-ref-format", "--branch", name],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        return result.Succeeded;
    }

    // ---- Remotes (G6). Network: may take long, so they take progress + cancellation. -------------

    /// <summary>The configured remotes ("origin", …), in git's order.</summary>
    public async Task<IReadOnlyList<string>> GetRemotesAsync(CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath, ["remote"], new GitRunOptions { ReadOnly = true }, ct);
        return result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Downloads new commits and branches from every remote (<c>fetch --all --prune</c>: remote
    /// branches deleted on the server disappear here too). Changes no local branch or file.
    /// </summary>
    public Task FetchAsync(IProgress<GitProgress>? progress = null, CancellationToken ct = default) =>
        RunRemoteAsync(["fetch", "--all", "--prune", "--progress"], progress, ct);

    /// <summary>
    /// Brings the current branch up to date with its upstream. <see cref="PullMode.FastForwardOnly"/>
    /// (the default) never creates a merge commit and fails with <see cref="RemoteErrorKind.Diverged"/>
    /// when both sides have new commits; <see cref="PullMode.Rebase"/> then replays the local
    /// commits on top, stashing uncommitted changes around it (<c>--autostash</c>).
    /// </summary>
    public Task PullAsync(PullMode mode = PullMode.FastForwardOnly, IProgress<GitProgress>? progress = null,
        CancellationToken ct = default) =>
        RunRemoteAsync(mode == PullMode.Rebase
                ? ["pull", "--rebase", "--autostash", "--progress"]
                : ["pull", "--ff-only", "--progress"],
            progress, ct);

    /// <summary>
    /// Pushes the current branch. With an upstream it goes there, even if the remote branch has
    /// another name (<c>push.default=upstream</c> for this command only). Without one, pass
    /// <paramref name="newUpstreamRemote"/>: the branch is pushed under its own name and starts
    /// tracking it (<c>-u</c>). <paramref name="forceWithLease"/> overwrites the remote branch,
    /// but only if it's still where we last saw it (never a blind <c>--force</c>).
    /// </summary>
    public Task PushAsync(string? newUpstreamRemote = null, bool forceWithLease = false,
        IProgress<GitProgress>? progress = null, CancellationToken ct = default)
    {
        List<string> args = ["-c", "push.default=upstream", "push", "--progress"];
        if (forceWithLease)
            args.Add("--force-with-lease");
        if (newUpstreamRemote is not null)
            args.AddRange(["-u", newUpstreamRemote, "HEAD"]);
        return RunRemoteAsync(args, progress, ct);
    }

    /// <summary>
    /// The commits a push would send: on the current branch but not on its upstream, newest
    /// first (at most <paramref name="maxCount"/>). Empty without an upstream.
    /// </summary>
    public async Task<IReadOnlyList<GitCommit>> GetUnpushedCommitsAsync(int maxCount = 100, CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath,
            ["log", "--no-color", "--format=" + LogParser.Format,
             "--max-count=" + maxCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
             "@{upstream}..HEAD", "--"],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        return result.Succeeded ? LogParser.Parse(result.StandardOutput) : [];
    }

    /// <summary>
    /// Runs a network command, reporting progress lines. A failure throws
    /// <see cref="RemoteOperationException"/> with the error explained.
    /// </summary>
    private async Task RunRemoteAsync(IReadOnlyList<string> args, IProgress<GitProgress>? progress, CancellationToken ct)
    {
        var lines = progress is null
            ? null
            : new LineProgress(line =>
            {
                if (ProgressParser.Parse(line) is { } p)
                    progress.Report(p);
            });
        try
        {
            // Not AllowFailure: a failed fetch/push is a real failure and shows red in the Git Console.
            await runner.RunAsync(RepositoryPath, args, new GitRunOptions { StandardErrorLines = lines }, ct);
        }
        catch (GitException ex)
        {
            throw new RemoteOperationException(RemoteErrorExplainer.Explain(ex.StandardError), ex);
        }
    }

    /// <summary>An IProgress that calls back synchronously (on git's reader thread), unlike Progress&lt;T&gt;.</summary>
    private sealed class LineProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    // ---- History (G5) ---------------------------------------------------------------------------

    /// <summary>
    /// The newest <paramref name="maxCount"/> commits reachable from any branch, remote branch,
    /// tag or HEAD, children before parents (<c>--topo-order</c>, which the graph layout needs).
    /// Stashes and notes are left out. An empty repository gives an empty list.
    /// </summary>
    public async Task<IReadOnlyList<GitCommit>> GetLogAsync(int maxCount, CancellationToken ct = default)
    {
        // --exclude applies to the --all after it. HEAD is included by --all (detached HEAD too).
        var result = await runner.RunAsync(RepositoryPath,
            ["log", "--no-color", "--topo-order", "--decorate=full", "--format=" + LogParser.Format,
             "--max-count=" + maxCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
             "--exclude=refs/stash", "--exclude=refs/notes/*", "--all"],
            new GitRunOptions { ReadOnly = true, AllowFailure = true }, ct);
        if (result.Succeeded)
            return LogParser.Parse(result.StandardOutput);
        // A repository without commits: nothing to show (any other failure is a real error).
        if (await GetBranchAsync(ct) is null)
            return [];
        throw new GitException(["log"], result.ExitCode, result.StandardError);
    }

    /// <summary>A commit's full message (subject, blank line, body).</summary>
    public async Task<string> GetCommitMessageAsync(string commitId, CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath, ["log", "-1", "--format=%B", commitId, "--"],
            new GitRunOptions { ReadOnly = true }, ct);
        return result.StandardOutput.TrimEnd();
    }

    /// <summary>
    /// Files a commit changed, compared with its first parent (for a merge: what the merge
    /// brought into the branch), or with nothing for a root commit. Renames are detected.
    /// </summary>
    public async Task<IReadOnlyList<FileChange>> GetCommitChangesAsync(GitCommit commit, CancellationToken ct = default)
    {
        var result = await runner.RunAsync(RepositoryPath,
            ["diff-tree", "-r", "-M", "--no-commit-id", "--name-status", "-z", .. CommitRange(commit)],
            new GitRunOptions { ReadOnly = true }, ct);
        return LogParser.ParseNameStatus(result.StandardOutput);
    }

    /// <summary>The diff of one file in a commit (same comparison as <see cref="GetCommitChangesAsync"/>).</summary>
    public async Task<FileDiff> GetCommitDiffAsync(GitCommit commit, FileChange change, CancellationToken ct = default,
        int maxLines = DiffParser.DefaultMaxLines)
    {
        List<string> args = ["diff-tree", "-p", "-r", "-M", "--no-commit-id", .. CommonDiffArgs, .. CommitRange(commit), "--", change.Path];
        if (change.OriginalPath is { } from)
            args.Add(from); // a rename needs both paths, or git shows an add and a delete

        var result = await runner.RunAsync(RepositoryPath, args, new GitRunOptions { ReadOnly = true }, ct);
        return DiffParser.Parse(result.StandardOutput, maxLines).FirstOrDefault()
               ?? new FileDiff { OldPath = change.OriginalPath ?? change.Path, NewPath = change.Path };
    }

    /// <summary>diff-tree arguments: "parent commit" for a normal or merge commit, "--root commit" for a root commit.</summary>
    private static string[] CommitRange(GitCommit commit) =>
        commit.ParentIds.Count > 0 ? [commit.ParentIds[0], commit.Id] : ["--root", commit.Id];

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
