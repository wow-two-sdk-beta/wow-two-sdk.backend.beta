namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>Outcome of a repository-existence probe — separates a genuinely missing repo from one the token can't see.</summary>
public enum RepoCheck
{
    /// <summary>The repo exists and is visible to the current token → 200.</summary>
    Exists,

    /// <summary>No such repo, or it's private and the token can't see it → 404.</summary>
    NotFound,

    /// <summary>The token is missing/expired or lacks the <c>repo</c> scope → 401/403.</summary>
    Unauthorized,

    /// <summary>The check could not be completed (transport error or an unexpected status).</summary>
    Failed
}

/// <summary>Outcome of probing for a file at a path in a repo → tells whether a marker/config file is present.</summary>
public enum FileCheck
{
    /// <summary>The file exists at the requested path → 200.</summary>
    Present,

    /// <summary>No such file at the path → 404.</summary>
    Absent,

    /// <summary>The token is missing/expired or can't see the repo → 401/403.</summary>
    Unauthorized,

    /// <summary>The check could not be completed (transport error or an unexpected status).</summary>
    Failed
}

/// <summary>Outcome of a releases lookup — separates a repo with no releases from one the token can't see or a transport failure.</summary>
public enum ReleaseLookup
{
    /// <summary>One or more releases were resolved → <see cref="ReleaseList.Releases"/> is populated.</summary>
    Found,

    /// <summary>The repo exists but has published no releases yet → 404 on the releases endpoint.</summary>
    None,

    /// <summary>The token is missing/expired or can't see the repo → 401/403.</summary>
    Unauthorized,

    /// <summary>The lookup could not be completed (transport error or an unexpected status).</summary>
    Failed
}

/// <summary>A single published GitHub release.</summary>
/// <param name="Tag">The release tag (e.g. <c>v1.2.0</c>).</param>
/// <param name="PublishedAtUtc">When the release was published, when reported.</param>
public sealed record ReleaseInfo(string Tag, DateTimeOffset? PublishedAtUtc);

/// <summary>The outcome of a releases lookup — the categorized result plus any resolved releases.</summary>
/// <param name="Outcome">How the lookup resolved.</param>
/// <param name="Releases">The releases, newest first; empty unless <paramref name="Outcome"/> is <see cref="ReleaseLookup.Found"/>.</param>
public sealed record ReleaseList(ReleaseLookup Outcome, IReadOnlyList<ReleaseInfo> Releases)
{
    /// <summary>An empty list result for a non-<see cref="ReleaseLookup.Found"/> outcome.</summary>
    /// <param name="outcome">The categorized lookup outcome.</param>
    /// <returns>A <see cref="ReleaseList"/> carrying no releases.</returns>
    public static ReleaseList Empty(ReleaseLookup outcome)
    {
        return new ReleaseList(outcome, []);
    }
}

/// <summary>Outcome of reading the latest run of a workflow — distinguishes succeeded / failed / running / never-ran / unreadable.</summary>
public enum BuildRunCheck
{
    /// <summary>The latest run completed successfully.</summary>
    Succeeded,

    /// <summary>The latest run completed unsuccessfully (failed, cancelled, or timed out).</summary>
    Failed,

    /// <summary>A run is queued or in progress.</summary>
    Running,

    /// <summary>The workflow has no runs yet.</summary>
    None,

    /// <summary>The token is missing/expired or can't see the repo → 401/403.</summary>
    Unauthorized,

    /// <summary>The check could not be completed (transport error or an unexpected status).</summary>
    ProbeFailed
}

/// <summary>Reads repository metadata from the GitHub REST API, authorizing each call with the configured <see cref="IAccessTokenProvider"/> so visibility matches that token.</summary>
public interface IGitHubClient
{
    /// <summary>Probes whether <paramref name="repo"/> (an <c>{owner}/{repo}</c> reference) exists and is visible to the current token.</summary>
    /// <param name="repo">The <c>{owner}/{repo}</c> reference to check.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="RepoCheck"/> categorizing the outcome.</returns>
    Task<RepoCheck> RepoExistsAsync(string repo, CancellationToken ct);

    /// <summary>Probes whether <paramref name="repo"/> carries a file at <paramref name="path"/> (repo-relative).</summary>
    /// <param name="repo">The <c>{owner}/{repo}</c> reference to check.</param>
    /// <param name="path">The repo-relative file path (e.g. <c>.github/workflows/ci.yml</c>).</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="FileCheck"/> categorizing the outcome.</returns>
    Task<FileCheck> FileExistsAsync(string repo, string path, CancellationToken ct);

    /// <summary>Gets the latest published release of <paramref name="repo"/>.</summary>
    /// <param name="repo">The <c>{owner}/{repo}</c> reference to read.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ReleaseList"/> whose <see cref="ReleaseList.Releases"/> holds the single latest release when found.</returns>
    Task<ReleaseList> GetLatestReleaseAsync(string repo, CancellationToken ct);

    /// <summary>Gets up to <paramref name="limit"/> of <paramref name="repo"/>'s releases, newest first.</summary>
    /// <param name="repo">The <c>{owner}/{repo}</c> reference to read.</param>
    /// <param name="limit">The maximum number of releases to return (clamped to 1–100).</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ReleaseList"/> carrying the resolved releases, newest first.</returns>
    Task<ReleaseList> GetReleasesAsync(string repo, int limit, CancellationToken ct);

    /// <summary>Reads the conclusion of the latest run of workflow <paramref name="workflowFile"/> in <paramref name="repo"/>.</summary>
    /// <param name="repo">The <c>{owner}/{repo}</c> reference to read.</param>
    /// <param name="workflowFile">The workflow file name (e.g. <c>ci.yml</c>) whose runs are queried via the Actions API.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="BuildRunCheck"/> categorizing the latest run.</returns>
    Task<BuildRunCheck> GetLatestWorkflowRunAsync(string repo, string workflowFile, CancellationToken ct);
}
