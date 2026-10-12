using System.Net;

namespace DevHub.Integration;

/// <summary>A call to GitHub's REST API through <see cref="IGitHubApi"/>.</summary>
/// <param name="Method">GET, POST, PATCH, PUT or DELETE.</param>
/// <param name="Path">Relative to https://api.github.com/ ("repos/naurid/devhub/actions/runs?per_page=20"), or a full
/// https://api.github.com/ URL (e.g. the next page from <see cref="GitHubApiResponse.NextPageUrl"/>).</param>
/// <param name="Body">A JSON body, or null.</param>
/// <param name="Account">The GitHub login to call as (one signed in through Git Credential Manager); null: the default
/// account (the only one, or the first).</param>
/// <param name="IfNoneMatch">An ETag from an earlier response: GitHub answers 304 Not Modified (free of the rate limit) when nothing changed.</param>
/// <param name="Accept">The Accept header, if not "application/vnd.github+json" (e.g. raw logs).</param>
public sealed record GitHubApiRequest(string Method, string Path, string? Body = null, string? Account = null, string? IfNoneMatch = null, string? Accept = null)
{
    public static GitHubApiRequest Get(string path, string? ifNoneMatch = null, string? account = null) => new("GET", path, null, account, ifNoneMatch);
}

/// <param name="Body">The response body as text (JSON, or a log), empty for 204 / 304.</param>
/// <param name="ETag">For the next request's <see cref="GitHubApiRequest.IfNoneMatch"/>.</param>
/// <param name="NextPageUrl">The "next" page from the Link header, or null on the last page.</param>
/// <param name="RateLimitRemaining">Requests left this hour (x-ratelimit-remaining), when GitHub says.</param>
/// <param name="RateLimitReset">When the limit resets (x-ratelimit-reset), when GitHub says.</param>
public sealed record GitHubApiResponse(HttpStatusCode Status, string Body, string? ETag, string? NextPageUrl,
    int? RateLimitRemaining, DateTimeOffset? RateLimitReset)
{
    public bool IsSuccess => (int)Status is >= 200 and < 300;
    public bool NotModified => Status == HttpStatusCode.NotModified;
}

/// <summary>Why a GitHub call couldn't be made at all (nobody signed in, no network); an HTTP error is a response instead.</summary>
public sealed class GitHubApiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// GitHub's REST API, called as the signed-in user. Offered by the GitHub plugin (which signs in through Git Credential
/// Manager); used by plugins that need GitHub (CI's runs and releases, Board's GitHub Issues import), which declare
/// GitHub in their Requires or treat it as optional. The token never leaves the GitHub plugin: requests go through
/// here, only to api.github.com. Callers pace themselves (ETags, the rate limit fields).
/// </summary>
public interface IGitHubApi
{
    /// <summary>The GitHub logins signed in through Git Credential Manager (empty: nobody).</summary>
    Task<IReadOnlyList<string>> GetAccountsAsync(CancellationToken ct = default);

    /// <summary>Sends the request; an HTTP error comes back as a response (check <see cref="GitHubApiResponse.IsSuccess"/>).</summary>
    /// <exception cref="GitHubApiUnavailableException">No account, or GitHub can't be reached.</exception>
    Task<GitHubApiResponse> SendAsync(GitHubApiRequest request, CancellationToken ct = default);
}
