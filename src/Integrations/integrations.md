# WoW.Two.Sdk.Backend.Beta.Integrations

> Typed HTTP clients for third-party services (GitHub REST, GHCR). Each authorizes its calls with a pluggable `IAccessTokenProvider` — the token source is yours to choose.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta
```

## Register

```csharp
builder.Services.AddHttpContextAccessTokenProvider();   // token source (signed-in user's access_token)
builder.Services.AddGitHubIntegration();                // IGitHubClient
builder.Services.AddGhcrIntegration();                  // IContainerRegistryClient
```

## `IAccessTokenProvider` — the parameterized token source

Both clients resolve their per-call Bearer/Basic token from this one contract:

```csharp
public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);   // null → unauthorized
}
```

- **Default:** `HttpContextAccessTokenProvider` reads the `access_token` saved on the current request's auth ticket (`SaveTokens = true`); `null` when there's no context or token. Register via `AddHttpContextAccessTokenProvider()` (adds `IHttpContextAccessor`).
- **Custom:** register your own `IAccessTokenProvider` (a PAT from config, a service token, a machine identity) before the integration calls. Off-request callers (background jobs) need a non-HttpContext provider.

## Clients

| Client | Interface | Register | Auth |
|---|---|---|---|
| GitHub REST | `IGitHubClient` | `AddGitHubIntegration()` | per-call `Bearer` from the provider |
| GHCR | `IContainerRegistryClient` | `AddGhcrIntegration()` | anon pull token → `Basic`-authed pull token (private) |

Every method returns a categorized outcome enum (`RepoCheck`, `FileCheck`, `ImageCheck`, …) — resilient, never throws on transport/HTTP errors; failures are logged and folded into a `Failed`/`ProbeFailed` value.

## See also

- `GitHub/github.md` — GitHub REST client surface.
- `Ghcr/ghcr.md` — GHCR client + the two-token pull flow.
- `Identity/OAuth` — provider sign-in that saves the `access_token` the default provider reads.
