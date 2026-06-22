# wow-two-sdk.backend.beta

> Beta-forever .NET 9 backend SDK — wraps the .NET ecosystem behind opinionated, descriptively-named registration extensions (`AddJwtBearerAuthentication`, `AddOpenTelemetryTracing`, `AddPerIpSlidingWindowRateLimit`, …) so a `Program.cs` becomes 1–2 liners per concern instead of 100.

## Status

| Phase | Bundle | Status |
|---|---|---|
| P0 | Testing scaffold (parallel track) | ✅ 12 packages shipped |
| P1 — foundation | `time`, `errors`, `results`, `validation`, `serialization`, `guards`, `value-objects` | ✅ 7 packages shipped |
| P1 — observability | `logging`, `tracing`, `metrics`, `healthchecks`, `otlp`, `prometheus`, `azure-monitor`, `datadog` | ✅ 8 packages shipped |
| P1 — web | `hosting`, `openapi`, `problemdetails`, `ratelimit`, `outputcache`, `secureheaders`, `cors`, `compression`, `versioning` | ✅ 9 packages shipped |
| P2 — mediator | `mediator`, `mediator.{validation, logging, authorization, idempotency}` | ✅ 5 packages shipped |
| P2 — identity | `identity.{jwt, cookies, oidc, identity-api}`, `oauth.{16 providers}`, `mfa.{totp, webauthn}`, `password-hashing.argon2`, `otp` + `otp.telegram`, `jwt.issuance`, `policies` | ✅ shipped |
| P3 | persistence + outbound | 🚧 `data` ✅ · `http` ✅ · `caching` deferred |
| P4 | distributed essentials | 🚧 `comms/email` ✅ · `jobs/hangfire(+postgres)` ✅ · messaging + webhooks planned |
| meta | `AddApiDefaults()` / `UseApiDefaults()` one-import boot floor | ✅ |
| P5 | SaaS-shaped (tenancy + AI + flags) | planned |
| P6 | heavy domain extensions | planned |

Ships as a **mono-lib** — one NuGet (`WoW2.Sdk.Backend.Beta`) + a separate `.Testing` lib; per-area subpaths inside. Area-by-area status: [`docs/package-registry.md`](./docs/package-registry.md).

OAuth sign-in providers (16): Google, Microsoft, GitHub, Apple, Facebook, LinkedIn, Discord, Slack, GitLab, Amazon, Twitch, Spotify, Yandex, Reddit, Notion, VK. Passwordless: `AddOtpService` + `AddTelegramOtpDelivery` + `AddJwtTokenIssuance` + `AddRolePolicy`.

## Active design work

In-progress patterns being designed/built — read these to know what's underway:

- [`docs/analysis/validation-and-result-pattern.md`](./docs/analysis/validation-and-result-pattern.md) — validation result model (Stage 1, building now) + the deferred generic `Result`/`Error` pattern.

## Quick start

```csharp
using WoW.Two.Sdk.Backend.Beta;

var builder = WebApplication.CreateBuilder(args);

// One call = serilog + otel + health + proxy hosting + openapi + problemdetails
//            + rate limit + output cache + compression (all flag-off-able).
builder.AddApiDefaults(o => o.ValidatorAssemblies.Add(typeof(Program).Assembly));

// Per-app concerns stay explicit:
builder.Services
    .AddMediator(typeof(Program).Assembly)
    .AddMediatorValidationBehavior()
    .AddMediatorLoggingBehavior()
    .AddJwtBearerAuthentication(o =>
    {
        o.Issuer   = "https://issuer";
        o.Audience = "my-api";
        o.JwksUri  = new Uri("https://issuer/.well-known/openid-configuration");
    });

var app = builder.Build();
app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "ok");
app.Run();
```

Need per-concern control? Every piece `AddApiDefaults` wires is a public extension you can call
directly — the expansion is listed in [`src/Meta/Meta.md`](./src/Meta/Meta.md).

## Read these first

- [`docs/analysis/philosophy/ideas.md`](./docs/analysis/philosophy/ideas.md) — encyclopedia of the .NET ecosystem (no verdicts)
- [`docs/analysis/philosophy/targets.md`](./docs/analysis/philosophy/targets.md) — verdicts (DONE / NOW / NEXT / LATER / MAYBE / SKIP / LOCKED)
- [`docs/architecture/package-layout.md`](./docs/architecture/package-layout.md) — repo + per-package shape + three-layer doc strategy

## Build

```bash
cd src
ulimit -n 65535          # macOS — avoid EMFILE
export MSBUILDDISABLENODEREUSE=1
dotnet restore WoW.Two.Sdk.Backend.Beta.slnx -m:1
dotnet build WoW.Two.Sdk.Backend.Beta.slnx --no-restore -m:1
```

## License

MIT.
