namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>Defines behavior that reads repository metadata from the GitHub REST API, authorizing each call with the configured <see cref="IAccessTokenService"/> so visibility matches that token.</summary>
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
