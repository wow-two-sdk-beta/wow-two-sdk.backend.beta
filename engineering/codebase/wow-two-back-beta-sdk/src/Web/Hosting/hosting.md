# WoW.Two.Sdk.Backend.Beta.Web.Hosting

> ASP.NET Core hosting plumbing — forwarded headers + request decompression with sane defaults.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.Hosting
```

## Usage

```csharp
builder.Services.AddProxyAwareHosting(options =>
{
    options.AllowedHosts.Add("app.example.com");
    options.TrustedNetworks.Add("172.16.0.0/12");   // the ingress network, when it is not loopback
});

var app = builder.Build();
app.UseProxyAwareHosting();   // adds forwarded headers + request decompression — call early
```

## Proxy trust

- `X-Forwarded-For`, `-Proto` and `-Host` apply only from loopback, `TrustedProxies` and `TrustedNetworks`.
- Any other sender keeps its socket address, so a client cannot spoof its IP, scheme or host.
- `ForwardLimit` (default 1) unwinds one proxy hop; raise it only for a known proxy chain.
- `AddApiDefaults` passes `TrustedProxies` and `TrustedNetworks` through from `ApiDefaultsOptions`.

## Probe hosts

- `ProbeHosts` (default `localhost`) join any restricted host allowlist, from options or the `AllowedHosts` setting.
- A container health check can call `http://localhost:8080/health` without the public host header.
- A wildcard allowlist is left unchanged; clear `ProbeHosts` to admit only the listed hosts.

## Related

- Persistent Data Protection keys: [data protection](../DataProtection/data-protection.md).
- Database readiness: `AddDatabaseReadinessCheck<TContext>()` in [health checks](../../Observability/HealthChecks/health-checks.md).

## SPA single-host serving

Serve a React/Vite (or any) SPA bundle from the same host as the API. `UseSpaHosting` serves the static
bundle (call early); `MapSpaFallback` 404s unmatched `/api/*` as JSON and falls every other unmatched
route back to the shell (call after your endpoints).

```csharp
var app = builder.Build();

app.UseSpaHosting();      // default document + static files — before auth/endpoints so assets short-circuit
app.UseApiDefaults();     // SDK pipeline
app.MapControllers();     // your endpoints first, so real routes win
app.MapSpaFallback();     // /api/* → JSON 404; everything else → index.html
```

`SpaHostingOptions`: `ApiPathPrefix` (default `/api`), `FallbackFile` (default `index.html`),
`ServeDefaultFiles` (default `true`). Configure via the optional callback on either call:
`app.MapSpaFallback(o => o.ApiPathPrefix = "/v1");`.
