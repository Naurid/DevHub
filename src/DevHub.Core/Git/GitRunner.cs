using System.Diagnostics;
using System.Text;

namespace DevHub.Core.Git;

/// <summary>What a git command produced.</summary>
public sealed record GitResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Per-command options for <see cref="GitRunner.RunAsync"/>.</summary>
public sealed record GitRunOptions
{
    /// <summary>Text written to git's stdin (e.g. a commit message for <c>commit -F -</c>).</summary>
    public string? StandardInput { get; init; }

    /// <summary>
    /// The command only reads. Sets GIT_OPTIONAL_LOCKS=0 so it never takes locks that would
    /// get in the way of git commands the user runs in a terminal at the same time.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>Return a failed result instead of throwing <see cref="GitException"/>.</summary>
    public bool AllowFailure { get; init; }
}

/// <summary>A git command exited with a non-zero code.</summary>
public sealed class GitException(IReadOnlyList<string> arguments, int exitCode, string standardError)
    : Exception($"git {string.Join(' ', arguments)} failed ({exitCode}): {standardError.Trim()}")
{
    public IReadOnlyList<string> Arguments { get; } = arguments;
    public int ExitCode { get; } = exitCode;
    public string StandardError { get; } = standardError;
}

/// <summary>
/// The only way DevHub runs git (see docs/GIT_CLIENT_PLAN.md, "Rules"): one process per
/// command, arguments passed as a list (never a shell string), no interactive prompts, every
/// run recorded in the <see cref="IGitCommandLog"/>.
/// </summary>
public sealed class GitRunner
{
    private const int LoggedOutputLength = 2000;

    /// <summary>Added before every command: print paths as-is (UTF-8) instead of octal escapes.</summary>
    private static readonly string[] CommonOptions = ["-c", "core.quotepath=false"];

    private readonly GitExecutable _git;
    private readonly IGitCommandLog _log;
    private readonly IReadOnlyDictionary<string, string?> _environment;

    /// <param name="git">The git program to run.</param>
    /// <param name="log">Receives every command.</param>
    /// <param name="environment">
    /// Extra environment variables for every command (null value = remove the variable).
    /// Tests use it to isolate git from the user's own configuration.
    /// </param>
    public GitRunner(GitExecutable git, IGitCommandLog log, IReadOnlyDictionary<string, string?>? environment = null)
    {
        _git = git;
        _log = log;
        _environment = environment ?? new Dictionary<string, string?>();
    }

    /// <summary>
    /// Runs <c>git &lt;arguments&gt;</c> in <paramref name="workingDirectory"/> and waits for it.
    /// Throws <see cref="GitException"/> on a non-zero exit unless <see cref="GitRunOptions.AllowFailure"/>;
    /// throws <see cref="OperationCanceledException"/> (after killing git) when cancelled.
    /// </summary>
    public async Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        GitRunOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new GitRunOptions();

        var psi = new ProcessStartInfo(_git.Path)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = options.StandardInput is not null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var option in CommonOptions)
            psi.ArgumentList.Add(option);
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        // Never wait for a password/username prompt nobody can see.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        if (options.ReadOnly)
            psi.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var (name, value) in _environment)
        {
            if (value is null)
                psi.Environment.Remove(name);
            else
                psi.Environment[name] = value;
        }

        var started = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process { StartInfo = psi };
        process.Start();

        // Start reading both streams before anything else: if git fills a pipe nobody reads
        // (stdout, stderr) while we're still writing stdin, both sides would wait forever.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            if (options.StandardInput is not null)
            {
                // UTF-8 without BOM, so the first line of a commit message isn't polluted.
                await using var stdin = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false));
                await stdin.WriteAsync(options.StandardInput.AsMemory(), ct);
            }

            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            _log.Add(new GitCommandEntry(workingDirectory, arguments, started, stopwatch.Elapsed, -1, "(cancelled)"));
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var result = new GitResult(process.ExitCode, stdout, stderr, stopwatch.Elapsed);

        var shown = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        _log.Add(new GitCommandEntry(workingDirectory, arguments, started, result.Duration, result.ExitCode,
            shown.Length > LoggedOutputLength ? shown[..LoggedOutputLength] + "…" : shown,
            FailureExpected: options.AllowFailure));

        if (!result.Succeeded && !options.AllowFailure)
            throw new GitException(arguments, result.ExitCode, stderr);

        return result;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill.
        }
    }
}
