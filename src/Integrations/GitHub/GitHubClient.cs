using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>Integrates with the GitHub REST API over a typed <see cref="HttpClient"/>, authorizing each call with the token from <see cref="IAccessTokenProvider"/> so repo visibility matches that token.</summary>
internal sealed class GitHubClient(
    HttpClient http,
    IAccessTokenProvider tokenProvider,
    ILogger<GitHubClient> logger) : IGitHubClient
{
    /// <inheritdoc />
    public async Task<RepoCheck> RepoExistsAsync(string repo, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
            return RepoCheck.Unauthorized;

        var response = await SendAsync(HttpMethod.Get, $"repos/{repo}", token, repo, ct);
        if (response is null)
            return RepoCheck.Failed;

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.OK => RepoCheck.Exists,
                HttpStatusCode.NotFound => RepoCheck.NotFound,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => RepoCheck.Unauthorized,
                _ => Unexpected(repo, response.StatusCode, RepoCheck.Failed)
            };
        }
    }

    /// <inheritdoc />
    public async Task<FileCheck> FileExistsAsync(string repo, string path, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
            return FileCheck.Unauthorized;

        var response = await SendAsync(HttpMethod.Get, $"repos/{repo}/contents/{path}", token, repo, ct);
        if (response is null)
            return FileCheck.Failed;

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.OK => FileCheck.Present,
                HttpStatusCode.NotFound => FileCheck.Absent,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => FileCheck.Unauthorized,
                _ => Unexpected(repo, response.StatusCode, FileCheck.Failed)
            };
        }
    }

    /// <inheritdoc />
    public async Task<ReleaseList> GetLatestReleaseAsync(string repo, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
            return ReleaseList.Empty(ReleaseLookup.Unauthorized);

        var response = await SendAsync(HttpMethod.Get, $"repos/{repo}/releases/latest", token, repo, ct);
        if (response is null)
            return ReleaseList.Empty(ReleaseLookup.Failed);

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var release = await ReadReleaseAsync(response, repo, ct);
                    return release is null
                        ? ReleaseList.Empty(ReleaseLookup.Failed)
                        : new ReleaseList(ReleaseLookup.Found, [release]);
                case HttpStatusCode.NotFound:
                    return ReleaseList.Empty(ReleaseLookup.None);
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    return ReleaseList.Empty(ReleaseLookup.Unauthorized);
                default:
                    return ReleaseList.Empty(Unexpected(repo, response.StatusCode, ReleaseLookup.Failed));
            }
        }
    }

    /// <inheritdoc />
    public async Task<ReleaseList> GetReleasesAsync(string repo, int limit, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
            return ReleaseList.Empty(ReleaseLookup.Unauthorized);

        var perPage = Math.Clamp(limit, 1, 100);
        var response = await SendAsync(HttpMethod.Get, $"repos/{repo}/releases?per_page={perPage}", token, repo, ct);
        if (response is null)
            return ReleaseList.Empty(ReleaseLookup.Failed);

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var releases = await ReadReleasesAsync(response, repo, ct);
                    return releases is null
                        ? ReleaseList.Empty(ReleaseLookup.Failed)
                        : new ReleaseList(releases.Count == 0 ? ReleaseLookup.None : ReleaseLookup.Found, releases);
                case HttpStatusCode.NotFound:
                    return ReleaseList.Empty(ReleaseLookup.None);
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    return ReleaseList.Empty(ReleaseLookup.Unauthorized);
                default:
                    return ReleaseList.Empty(Unexpected(repo, response.StatusCode, ReleaseLookup.Failed));
            }
        }
    }

    /// <inheritdoc />
    public async Task<BuildRunCheck> GetLatestWorkflowRunAsync(string repo, string workflowFile, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
            return BuildRunCheck.Unauthorized;

        var response = await SendAsync(
            HttpMethod.Get, $"repos/{repo}/actions/workflows/{workflowFile}/runs?per_page=1", token, repo, ct);
        if (response is null)
            return BuildRunCheck.ProbeFailed;

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return await ReadRunAsync(response, repo, ct);
                case HttpStatusCode.NotFound:
                    return BuildRunCheck.None;
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    return BuildRunCheck.Unauthorized;
                default:
                    return Unexpected(repo, response.StatusCode, BuildRunCheck.ProbeFailed);
            }
        }
    }

    private async Task<HttpResponseMessage?> SendAsync(
        HttpMethod method, string path, string token, string repo, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitHub call {Method} {Path} for {Repo} failed to reach the API.", method, path, repo);
            return null;
        }
    }

    private async Task<ReleaseInfo?> ReadReleaseAsync(HttpResponseMessage response, string repo, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GitHubRelease>(ct);
            return ToReleaseInfo(payload);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitHub release for {Repo} could not be parsed.", repo);
            return null;
        }
    }

    private async Task<IReadOnlyList<ReleaseInfo>?> ReadReleasesAsync(HttpResponseMessage response, string repo, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(ct);
            if (payload is null)
                return null;

            var releases = new List<ReleaseInfo>(payload.Count);
            foreach (var item in payload)
            {
                var info = ToReleaseInfo(item);
                if (info is not null)
                    releases.Add(info);
            }

            return releases;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitHub releases for {Repo} could not be parsed.", repo);
            return null;
        }
    }

    private async Task<BuildRunCheck> ReadRunAsync(HttpResponseMessage response, string repo, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GitHubWorkflowRuns>(ct);
            var run = payload?.Runs is { Count: > 0 } runs ? runs[0] : null;
            if (run is null)
                return BuildRunCheck.None;

            if (!string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase))
                return BuildRunCheck.Running;

            return string.Equals(run.Conclusion, "success", StringComparison.OrdinalIgnoreCase)
                ? BuildRunCheck.Succeeded
                : BuildRunCheck.Failed;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitHub workflow runs for {Repo} could not be parsed.", repo);
            return BuildRunCheck.ProbeFailed;
        }
    }

    private static ReleaseInfo? ToReleaseInfo(GitHubRelease? release)
    {
        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            return null;

        return new ReleaseInfo(release.TagName, release.PublishedAt);
    }

    private T Unexpected<T>(string repo, HttpStatusCode status, T failed)
    {
        logger.LogWarning("GitHub call for {Repo} returned an unexpected status {Status}.", repo, status);
        return failed;
    }

    /// <summary>The slice of a GitHub release payload a deployable version is keyed on.</summary>
    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt);

    /// <summary>The slice of a GitHub workflow-runs response read to tell build status.</summary>
    private sealed record GitHubWorkflowRuns(
        [property: JsonPropertyName("workflow_runs")] List<GitHubWorkflowRun>? Runs);

    /// <summary>The status fields of a single GitHub Actions workflow run.</summary>
    private sealed record GitHubWorkflowRun(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("conclusion")] string? Conclusion);
}
