# Identity.ApiKeys

> API keys for outside programs — marked secrets stored as hashes, a scheme that reads Bearer or `X-Api-Key`, and
> a gate that lets this machine through while everyone else presents a live key.

Extracted from TranscriptForge v0.7 (public API and MCP access).

| Seam | Default |
|---|---|
| `IApiKeyStore` | **product-owned** — `FindLiveByHashAsync(hash)` and `TouchAsync(id, usedAt)` over its key table |
| `ApiKeySecretFactory` | `Create()` mints `{marker}` + 32 base62 characters; `ToHash` (SHA-256 hex), `ToPrefix`, `IsSecretShaped` |
| `ApiKeySecretReader` | a Bearer token carrying the marker, else the key header; any other Bearer is left to other schemes |
| `ApiKeyAuthenticationHandler` | scheme `ApiKey` — claims `api_key_id` and `Name`; last use written once per `TouchInterval` |
| `ApiKeyAccessGateMiddleware` | guarded / open / local-only paths; loopback passes without a key |

```csharp
builder.Services.AddScoped<IApiKeyStore, ApiKeyRepository>();
builder.Services.AddApiKeyAuthentication(
    keys => keys.Marker = "tf_",
    gate =>
    {
        gate.GuardedPaths.Add("/mcp");            // "/api" is guarded by default
        gate.OpenPaths.Add("/api/system/status"); // probes stay open
        gate.LocalOnlyPaths.Add("/api/keys");     // keys are managed from this machine only
    });

var app = builder.Build();
app.UseAuthentication();
app.UseApiKeyAccessGate();

// creating a key — store the hash and prefix, show the secret once
var key = secrets.Create();
await repository.AddAsync(new ApiKeyEntity { Name = name, Prefix = key.Prefix, Hash = key.Hash });
return key.Secret;
```

- A presented key is always checked; an unknown or revoked key is 401 even from this machine.
- A key calling a local-only path is 403; a remote caller without a key is 401 — both as ProblemDetails.
- The default authentication scheme is left alone, so JWT bearer or cookies keep theirs beside it.
- Contract: [ApiKeys.spec.md](ApiKeys.spec.md).
