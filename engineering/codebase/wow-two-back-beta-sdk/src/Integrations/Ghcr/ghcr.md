# WoW.Two.Sdk.Backend.Beta.Integrations.Ghcr

> Typed `HttpClient` over the GitHub Container Registry (GHCR) v2 API — confirms a published image tag exists, public or private.

## Register

```csharp
builder.Services.AddHttpContextAccessTokenProvider();   // or your own IAccessTokenProvider
builder.Services.AddGhcrIntegration();                  // UA wow-two-sdk
```

Configures a typed client → `IContainerRegistryClient` (UA `wow-two-sdk`).

## Use

```csharp
public sealed class Probe(IContainerRegistryClient registry)
{
    public Task<ImageCheck> Run(CancellationToken ct) =>
        registry.ImageExistsAsync("owner/repo", "v1.2.0", ct);   // ghcr.io/owner/repo:v1.2.0
}
```

## Pull-token flow

Each `ImageExistsAsync` probe:

1. Fetch an **anonymous** GHCR pull token and `HEAD` the manifest — covers public images, no token needed.
2. On `401/403`, mint a pull token authenticated with the `IAccessTokenProvider` token (needs `read:packages`) via GHCR's `Basic` realm, then re-`HEAD` — covers private images.
3. Still refused → `ImageCheck.Unauthorized` (no provider token → returns early).

Image paths are lowercased (`repo.ToLowerInvariant()`) — GHCR is case-insensitive regardless of GitHub repo casing.

| Outcome | Meaning |
|---|---|
| `Exists` | manifest `HEAD` → 200 |
| `Missing` | manifest `HEAD` → 404 |
| `Unauthorized` | neither anon nor token could read it → 401/403 |
| `Failed` | transport error or unexpected status (logged, not thrown) |

## See also

- `IContainerRegistryClient.cs` — interface + `ImageCheck`.
- `../integrations.md` — `IAccessTokenProvider` token source.
- [Working with the Container registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry).
