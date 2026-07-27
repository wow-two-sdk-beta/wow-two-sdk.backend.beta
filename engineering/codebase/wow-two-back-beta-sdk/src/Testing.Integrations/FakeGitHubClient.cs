using WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Integrations;

/// <summary>
/// Configurable test double for <see cref="IGitHubClient"/> — short-circuits every GitHub probe so a test never
/// calls the real GitHub REST API (which would also need a signed-in user's OAuth token).
/// </summary>
/// <remarks>
/// Generalizes drydock's <c>StubGitHubClient</c>. Defaults to the happy path (<see cref="RepoCheck.Exists"/>,
/// file present, no releases, no workflow runs); flip the settable properties — or use the fluent
/// <c>With…</c> helpers — to drive the NotFound / Unauthorized / no-CI / never-built / latest-not-ready branches.
/// Each setting is a single canned outcome returned for every call, mirroring the proven stub; richer
/// per-repo behaviour is out of scope for the canned fake.
/// </remarks>
public sealed class FakeGitHubClient : IGitHubClient
{
    /// <summary>The canned outcome every <see cref="RepoExistsAsync"/> call returns. Defaults to <see cref="RepoCheck.Exists"/>.</summary>
    public RepoCheck Repo { get; set; } = RepoCheck.Exists;

    /// <summary>The canned outcome every <see cref="FileExistsAsync"/> call returns. Defaults to <see cref="FileCheck.Present"/>.</summary>
    public FileCheck File { get; set; } = FileCheck.Present;

    /// <summary>The releases the lookups return, newest first; drives both the latest and the list calls. Defaults to empty.</summary>
    public IReadOnlyList<ReleaseInfo> Releases { get; set; } = [];

    /// <summary>The outcome the lookups report when <see cref="Releases"/> is populated; override for the failure paths. Defaults to <see cref="ReleaseLookup.Found"/>.</summary>
    public ReleaseLookup ReleaseOutcome { get; set; } = ReleaseLookup.Found;

    /// <summary>The canned outcome every <see cref="GetLatestWorkflowRunAsync"/> call returns. Defaults to <see cref="BuildRunCheck.None"/>.</summary>
    public BuildRunCheck WorkflowRun { get; set; } = BuildRunCheck.None;

    /// <summary>Sets the canned <see cref="RepoExistsAsync"/> outcome and returns this instance for chaining.</summary>
    /// <param name="result">The repo-existence outcome to return for every call.</param>
    /// <returns>This <see cref="FakeGitHubClient"/>.</returns>
    public FakeGitHubClient WithRepo(RepoCheck result)
    {
        Repo = result;
        return this;
    }

    /// <summary>Sets the canned <see cref="FileExistsAsync"/> outcome and returns this instance for chaining.</summary>
    /// <param name="result">The file-existence outcome to return for every call.</param>
    /// <returns>This <see cref="FakeGitHubClient"/>.</returns>
    public FakeGitHubClient WithFile(FileCheck result)
    {
        File = result;
        return this;
    }

    /// <summary>Sets the releases (newest first) the lookups return, forcing <see cref="ReleaseOutcome"/> to <see cref="ReleaseLookup.Found"/>, and returns this instance for chaining.</summary>
    /// <param name="releases">The releases to return, newest first.</param>
    /// <returns>This <see cref="FakeGitHubClient"/>.</returns>
    public FakeGitHubClient WithReleases(params ReleaseInfo[] releases)
    {
        Releases = releases;
        ReleaseOutcome = ReleaseLookup.Found;
        return this;
    }

    /// <summary>Forces both release lookups to report <paramref name="outcome"/> (e.g. <see cref="ReleaseLookup.Unauthorized"/>) and returns this instance for chaining.</summary>
    /// <param name="outcome">The lookup outcome to force regardless of <see cref="Releases"/>.</param>
    /// <returns>This <see cref="FakeGitHubClient"/>.</returns>
    public FakeGitHubClient WithReleaseOutcome(ReleaseLookup outcome)
    {
        ReleaseOutcome = outcome;
        return this;
    }

    /// <summary>Sets the canned <see cref="GetLatestWorkflowRunAsync"/> outcome and returns this instance for chaining.</summary>
    /// <param name="result">The workflow-run outcome to return for every call.</param>
    /// <returns>This <see cref="FakeGitHubClient"/>.</returns>
    public FakeGitHubClient WithWorkflowRun(BuildRunCheck result)
    {
        WorkflowRun = result;
        return this;
    }

    /// <inheritdoc />
    public Task<RepoCheck> RepoExistsAsync(string repo, CancellationToken ct) => Task.FromResult(Repo);

    /// <inheritdoc />
    public Task<FileCheck> FileExistsAsync(string repo, string path, CancellationToken ct) => Task.FromResult(File);

    /// <inheritdoc />
    public Task<ReleaseList> GetLatestReleaseAsync(string repo, CancellationToken ct)
    {
        if (ReleaseOutcome is not ReleaseLookup.Found)
            return Task.FromResult(ReleaseList.Empty(ReleaseOutcome));
        if (Releases.Count == 0)
            return Task.FromResult(ReleaseList.Empty(ReleaseLookup.None));

        return Task.FromResult(new ReleaseList(ReleaseLookup.Found, [Releases[0]]));
    }

    /// <inheritdoc />
    public Task<ReleaseList> GetReleasesAsync(string repo, int limit, CancellationToken ct)
    {
        if (ReleaseOutcome is not ReleaseLookup.Found)
            return Task.FromResult(ReleaseList.Empty(ReleaseOutcome));
        if (Releases.Count == 0)
            return Task.FromResult(ReleaseList.Empty(ReleaseLookup.None));

        var page = Releases.Take(limit).ToList();
        return Task.FromResult(new ReleaseList(ReleaseLookup.Found, page));
    }

    /// <inheritdoc />
    public Task<BuildRunCheck> GetLatestWorkflowRunAsync(string repo, string workflowFile, CancellationToken ct) => Task.FromResult(WorkflowRun);
}
