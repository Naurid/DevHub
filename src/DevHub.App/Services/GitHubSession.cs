using DevHub.Core.GitHub;
using DevHub.ViewModels.Dialogs;

namespace DevHub.Services;

/// <summary>
/// The GitHub account for the whole app (Settings → GitHub, Publish, Clone): which accounts Git
/// Credential Manager has, signing in (GCM's window, in the status bar with Cancel) and out.
/// One place, so every screen offers the same "not signed in: sign in now?" path.
/// </summary>
/// <param name="github">Null when git isn't installed (everything then explains that).</param>
public sealed class GitHubSession(IDialogService dialogs, IStatusService status, GitHubService? github = null)
{
    public GitHubService? GitHub => github;

    public bool IsAvailable => github is not null;

    /// <summary>
    /// The signed-in accounts. Problems (GCM missing, git missing) go to the status bar and give
    /// an empty list.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListAccountsAsync()
    {
        if (github is null)
            return [];
        try
        {
            return await github.ListAccountsAsync();
        }
        catch (Exception ex)
        {
            status.Show("Couldn't read the GitHub accounts from Git Credential Manager: " + ex.Message);
            return [];
        }
    }

    /// <summary>Whether Git Credential Manager is installed (Git for Windows includes it).</summary>
    public async Task<bool> IsCredentialManagerAvailableAsync() =>
        github is not null && await github.Credentials.IsAvailableAsync();

    /// <summary>
    /// GCM's sign-in window (browser, device code or token: the user picks there). Waits in the
    /// status bar with Cancel. True when an account was added.
    /// </summary>
    public async Task<bool> SignInAsync()
    {
        if (github is null)
            return false;

        var before = await ListAccountsAsync();
        using var cts = new CancellationTokenSource();
        var task = status.StartTask("Signing in to GitHub (finish in the window that opened)…", cts.Cancel);
        try
        {
            await github.Credentials.SignInAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            status.Show("GitHub sign-in cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            status.Show("GitHub sign-in failed: " + FirstLine(ex));
            return false;
        }
        finally
        {
            task.Dispose();
        }

        var added = (await ListAccountsAsync()).Except(before).ToList();
        status.Show(added.Count > 0 ? $"Signed in to GitHub as {string.Join(", ", added)}." : "GitHub sign-in finished.");
        return added.Count > 0 || before.Count > 0;
    }

    /// <summary>Removes the account's sign-in from GCM, after asking.</summary>
    public async Task SignOutAsync(string account)
    {
        if (github is null)
            return;

        var confirm = new ConfirmDialogViewModel("Sign Out of GitHub",
            $"Remove the GitHub sign-in for {account} from Git Credential Manager? Pushing to and pulling from " +
            "github.com will ask you to sign in again (other apps using git share this sign-in).", "Sign Out");
        if (!await dialogs.ShowDialogAsync(confirm))
            return;

        try
        {
            await github.Credentials.SignOutAsync(account);
            status.Show($"Signed out {account}.");
        }
        catch (Exception ex)
        {
            status.Show("Sign-out failed: " + FirstLine(ex));
        }
    }

    /// <summary>
    /// The accounts, offering to sign in first when there are none (for Publish / Clone). Empty
    /// when the user declined or signing in failed.
    /// </summary>
    public async Task<IReadOnlyList<string>> RequireAccountsAsync(string purpose)
    {
        if (github is null)
        {
            status.Show("Git is not installed, so DevHub can't reach GitHub.");
            return [];
        }

        var accounts = await ListAccountsAsync();
        if (accounts.Count > 0)
            return accounts;

        var ask = new ConfirmDialogViewModel("Sign In to GitHub",
            $"To {purpose}, DevHub needs your GitHub account. Sign in now? Git Credential Manager opens a sign-in " +
            "window; git uses the same sign-in for pushing, and DevHub never stores your password or token.",
            "Sign In", destructive: false);
        if (!await dialogs.ShowDialogAsync(ask) || !await SignInAsync())
            return [];
        return await ListAccountsAsync();
    }

    private static string FirstLine(Exception ex) =>
        (ex is Core.Git.GitException git && git.StandardError.Trim() is { Length: > 0 } e ? e : ex.Message).Split('\n')[0].Trim();
}
