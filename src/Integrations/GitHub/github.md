# WoW.Two.Sdk.Backend.Beta.Integrations.GitHub

> Typed `HttpClient` over the GitHub REST API. Each call carries a `Bearer` token from `IAccessTokenProvider`, so repo visibility (public + private) matches that token.

## Register

```csharp
builder.Services.AddHttpContextAccessTokenProvider();   // or your own IAccessTokenProvider
builder.Services.AddGitHubIntegration();                // base https://api.github.com/, UA wow-two-sdk
```

Configures a typed client → `IGitHubClient` (base `https://api.github.com/`, UA `wow-two-sdk`, accept `application/vnd.github+json`).

## Use

```csharp
public sealed class Probe(IGitHubClient gh)
{
    public async Task Run(CancellationToken ct)
    {
        RepoCheck repo  = await gh.RepoExistsAsync("owner/repo", ct);
        FileCheck file  = await gh.FileExistsAsync("owner/repo", ".github/workflows/ci.yml", ct);
        ReleaseList rel = await gh.GetLatestReleaseAsync("owner/repo", ct);
        ReleaseList rs  = await gh.GetReleasesAsync("owner/repo", limit: 10, ct);
        BuildRunCheck run = await gh.GetLatestWorkflowRunAsync("owner/repo", "ci.yml", ct);
    }
}
```

## Surface

| Method | Returns | Notes |
|---|---|---|
| `RepoExistsAsync(repo, ct)` | `RepoCheck` | `Exists` / `NotFound` / `Unauthorized` / `Failed` |
| `FileExistsAsync(repo, path, ct)` | `FileCheck` | path is repo-relative; `Present` / `Absent` / … |
| `GetLatestReleaseAsync(repo, ct)` | `ReleaseList` | single latest release when `Found` |
| `GetReleasesAsync(repo, limit, ct)` | `ReleaseList` | newest first; `limit` clamped 1–100 |
| `GetLatestWorkflowRunAsync(repo, workflowFile, ct)` | `BuildRunCheck` | `workflowFile` e.g. `ci.yml`; `Succeeded` / `Failed` / `Running` / `None` / … |

`401/403 → Unauthorized`, `404 → NotFound`/`Absent`/`None`, transport + unexpected status → `Failed`/`ProbeFailed` (logged, never thrown).

## See also

- `IGitHubClient.cs` — interface + outcome types (`RepoCheck`, `FileCheck`, `ReleaseLookup`, `ReleaseInfo`, `ReleaseList`, `BuildRunCheck`).
- `../integrations.md` — `IAccessTokenProvider` token source.
- [GitHub REST API](https://docs.github.com/en/rest).
