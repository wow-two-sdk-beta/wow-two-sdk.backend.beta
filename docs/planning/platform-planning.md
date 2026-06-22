# Backend Beta — Platform Planning

*Last updated: 2026-06-10*

> Standing **roadmap + backlog** for the backend SDK. Format follows
> `conventions/planning/platform-planning/`. This is the durable home for everything we intend to
> build "lego-style" — orthogonal slices composed in host extensions. Deep-dives for non-trivial
> features live under `docs/planning/<feature>/`; phase verdicts stay in
> `docs/analysis/philosophy/targets.md`; shipped status stays in `docs/package-registry.md`.

## How this works

- **Roadmap** = the phase model (P0–P6) + cross-cutting tracks. One row per track, coarse status.
- **Backlog** = concrete buildable items not in flight. Type: `feature` · `issue` · `check` · `idea`.
- **Deep-dives** = a feature too big for one backlog row gets `docs/planning/<feature>/<feature>-architecture.md` and is linked from here.
- When an item ships → move its status here, update `package-registry.md`, sync `targets.md`.

## Roadmap

| Track | Scope | Status |
|---|---|---|
| P0 testing | host + container fixtures + verify/bogus/wiremock | ✅ shipped |
| P1 boot floor | foundation + observability + web + `AddApiDefaults` meta | ✅ shipped |
| P2 pipeline + auth | mediator + identity (jwt/cookies/oidc/oauth×16/mfa/argon2/otp/issuance/policies) | ✅ shipped |
| P3 persistence + outbound | data ✅ · http ✅ · caching ⏸️ (pre-fix needed) | 🚧 |
| **Identity rebuild** | own ASP.NET-Identity-compatible user model, sliced lego stores | ⏳ **next — see [identity/](identity/identity-architecture.md)** |
| Security batch | request-limits · token revocation · CSRF · HTTPS/HSTS · SSRF guard · lockout | ⏳ planned (partly subsumed by Identity rebuild) |
| P4 distributed | comms/email ✅ · jobs/hangfire ✅ · messaging/CAP ⏳ · webhooks ⏳ · sms/push ⏳ | 🚧 |
| P5 SaaS-shaped | tenancy · ai/core + vector · feature-flags | ⏳ planned |
| P6 heavy domain | realtime · storage · search · workflow · payments · geo | ⏳ planned |

## Backlog

| Item | Type | Track | Notes |
|---|---|---|---|
| Own identity (sliced user model + stores) | feature | Identity | Deep-dive: [identity/identity-architecture.md](identity/identity-architecture.md). Reverses the v1 "no user model" decision. |
| `web/request-limits` (body/header/timeout + decompression cap) | feature | Security | Closes the unbounded `AddRequestDecompression` we shipped in `AddProxyAwareHosting`. |
| Env-gate OpenAPI in `UseApiDefaults` | issue | Security | Currently maps OpenAPI flag-default-on → schema leak in Production (API9). |
| Wire real `AllowedHosts`/host-filtering in `AddProxyAwareHosting` | issue | Security | Doc comment claims host filtering; code never sets it. |
| HTTPS redirect into `UseApiDefaults` | feature | Security | HSTS ships; redirect does not. |
| Token revocation + refresh | feature | Identity | Folds into identity security-stamp; see deep-dive §11. |
| JWT validation hardening (alg allowlist, mandatory iss/aud/exp) | feature | Security | Harden existing `AddJwtBearerAuthentication`. |
| SSRF-safe outbound handler (deny-private-IP / allowlist) | feature | Security | On the existing HttpClient stack. |
| CSRF/antiforgery preset for cookie flows | feature | Security | Pairs with cookie sign-in. |
| Breached-password check (HIBP k-anonymity) | feature | Identity | Optional password validator slice. |
| caching slice (HybridCache + Redis L2 / FusionCache) | feature | P3 | Blocked on pre-fix discussion + decision 9.4. |
| messaging/CAP outbox + RabbitMQ transport | feature | P4 | Decision 9.2 (CAP vs Wolverine). |
| webhooks (HMAC sign/verify + replay window + SSRF guard) | feature | P4 | Don't ship webhooks unguarded — bundle security from day 1. |
| sms/push comms channels | feature | P4 | Twilio/Vonage SMS · FCM/APNS push. |
| ai/core (Microsoft.Extensions.AI) + SSE streaming + pgvector | feature | P5 | Pulled forward by transcript-forge. |
| Roslyn analyzer: foundation-can't-import-domain | check | Quality | Decision 9.14 open. |
| `apps/playground/` Aspire end-to-end smoke | check | Quality | Nothing validates wrappers end-to-end today. |
| Haven.Auth dogfood migration onto shipped OTP/issuance/policies | check | Identity | Validates the seams before products depend on them. |

## Deep-dives

| Feature | Doc | Status |
|---|---|---|
| Identity (own, sliced) | [identity/identity-architecture.md](identity/identity-architecture.md) | drafted |
