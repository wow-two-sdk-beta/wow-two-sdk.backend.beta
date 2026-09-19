# Backend Beta — Platform Planning

*Last updated: 2026-09-16*

> Standing **roadmap + backlog** for the backend SDK. Format follows
> `conventions/planning/platform-planning/`. This is the durable home for everything we intend to
> build "lego-style" — orthogonal slices composed in host extensions. Deep-dives for non-trivial
> features live under `engineering/planning/<feature>/`; phase verdicts stay in
> `engineering/architecture/analysis/philosophy/targets.md`; shipped status stays in `engineering/architecture/package-registry.md`.

## How this works

- **Roadmap** = the phase model (P0–P6) + cross-cutting tracks. One row per track, coarse status.
- **Backlog** = concrete buildable items not in flight. Type: `feature` · `issue` · `check` · `idea`.
- **Deep-dives** = a feature too big for one backlog row gets `engineering/planning/<feature>/<feature>-architecture.md` and is linked from here.
- When an item ships → move its status here, update `package-registry.md`, sync `targets.md`.

## Active sequence

1. Verify and publish the completed convention-sweep SDK release.
2. Repin every direct consumer and repair its used breaking APIs; apply ForeverPin's product track separately.
3. Build the residual data-session vector: shared EF/Dapper transaction, soft-delete/xmin read hardening, unit of work and commit hooks.
4. Complete translated error rendering: SDK resx mappers, fallback rules, plural/message formatting and pseudo-localization tests.

Request validation, aggregated validation failures and RFC 9457 ProblemDetails rendering already ship. They are inputs to translated error rendering, not a separate unfinished validation rebuild.

## Roadmap

| Track | Scope | Status |
|---|---|---|
| P0 testing | host + container fixtures + verify/bogus/wiremock | ✅ shipped |
| P1 boot floor | foundation + observability + web + `AddApiDefaults` meta | ✅ shipped |
| P2 pipeline + auth | mediator + identity (jwt/cookies/oidc/oauth×16/mfa/argon2/otp/issuance/policies) | ✅ shipped |
| P3 persistence + outbound | data ✅ · http ✅ · caching ⏸️ (pre-fix needed) | 🚧 |
| **Identity rebuild** | own ASP.NET-Identity-compatible user model, sliced lego stores | 🚧 **step 1 `identity/core` ✅ (2026-07-10)** — entities + user store + `UserAccountService` + `ApplyIdentitySchema` + `AddUserAccounts`; steps 2-10 next. See [identity/](identity/identity-architecture.md) |
| Security batch | request-limits · token revocation · CSRF · HTTPS/HSTS · SSRF guard · lockout | ⏳ planned (partly subsumed by Identity rebuild) |
| P4 distributed | comms/email ✅ · jobs/hangfire ✅ · **messaging ✅** (custom transport-port: in-mem/RabbitMQ/Kafka/NATS + EF outbox(+PG skip-locked) + EventSaga) · **webhooks ✅** (HMAC + SSRF guard) · CAP ⏳ · sms/push ⏳ | 🚧 |
| P5 SaaS-shaped | tenancy · ai/core + vector · feature-flags | ⏳ planned |
| P6 heavy domain | realtime · storage · search · workflow · payments · geo | ⏳ planned |

## Backlog

| Item | Type | Track | Notes |
|---|---|---|---|
| Own identity (sliced user model + stores) | feature | Identity | Deep-dive: [identity/identity-architecture.md](identity/identity-architecture.md). Reverses the v1 "no user model" decision. |
| `web/request-limits` (body/header/timeout + decompression cap) | feature | Security | **✅ shipped 2026-07-10** — `Web/RequestLimits` `AddRequestLimits`; called by `AddProxyAwareHosting` so decompression is bounded. Residual: decompressed-output hard cap. |
| Env-gate OpenAPI in `UseApiDefaults` | issue | Security | **✅ shipped 2026-07-10** — `ExposeOpenApi` now `bool?`; defaults to Development-only. |
| Wire real `AllowedHosts`/host-filtering in `AddProxyAwareHosting` | issue | Security | **✅ shipped 2026-07-10** — `ProxyAwareHostingOptions.AllowedHosts` → `HostFilteringOptions` + `UseHostFiltering`; doc corrected. |
| HTTPS redirect into `UseApiDefaults` | feature | Security | **✅ shipped 2026-07-10** — `EnableHttpsRedirection` (default on) → `UseHttpsRedirection` after forwarded-headers. |
| Token revocation + refresh | feature | Identity | Folds into identity security-stamp; see deep-dive §11. |
| JWT validation hardening (alg allowlist, mandatory iss/aud/exp) | feature | Security | Harden existing `AddJwtBearerAuthentication`. |
| SSRF-safe outbound handler (deny-private-IP / allowlist) | feature | Security | On the existing HttpClient stack. |
| CSRF/antiforgery preset for cookie flows | feature | Security | Pairs with cookie sign-in. |
| Breached-password check (HIBP k-anonymity) | feature | Identity | Optional password validator slice. |
| caching slice (HybridCache + Redis L2 / FusionCache) | feature | P3 | Blocked on pre-fix discussion + decision 9.4. |
| messaging transport-port + RabbitMQ/Kafka/NATS + EF outbox | feature | P4 | **✅ shipped 2026-07** — custom `Messaging/Transport` port (not CAP); RabbitMQ (native DLX) · Kafka + NATS (emulated DLQ) · EF outbox(+PG skip-locked) · EventSaga. CAP now positioned as one future adapter under the port (decision 9.2 superseded). |
| webhooks (HMAC sign/verify + replay window + SSRF guard) | feature | P4 | **✅ shipped 2026-07-10** — `Messaging/Webhooks`: HMAC-SHA256 (`timestamp.body`) + SSRF guard (scheme/host pre-flight + connect-time private-IP block). Replay window is receiver-side (timestamp header shipped). Residual: durable store + mgmt API. |
| sms/push comms channels | feature | P4 | Twilio/Vonage SMS · FCM/APNS push. |
| ai/core (Microsoft.Extensions.AI) + SSE streaming + pgvector | feature | P5 | Pulled forward by transcript-forge. |
| localization/translation completion | feature | P4 | Request culture, `.resx` loading and formatter bases ship. Add SDK `IErrorMessageMapper`/`IFieldErrorMessageMapper` resx implementations, fallback rules, plural/message formatting and pseudo-localization tests. |
| error completion | feature | P2/P4 | Core `AppError`, exception mapping, aggregated validation and ProblemDetails ship. Remaining SDK work: `HttpClientError`, result combinators, translated message keys and an error-code analyzer. Product error-catalog adoption belongs to the consumer pass. |
| **data session** (`IDataSession` + hooks + Dapper hardening + write guards) | feature | P3 | Researched + designed. Sweep fixed interceptor wiring, options mutability, tracked-instance replacement, generated Dapper tenant scope and test-provider interceptor preservation. Remaining: shared EF/Dapper transaction (D1), Dapper soft-delete/xmin reads (D2/D3), unit of work/hooks (D6) and full write guards. See deep-dive. |
| sweep consumer adoption | check | Release | After the new beta publishes, repin all direct ventures and the product template; repair only the APIs each consumer uses. ForeverPin's 31 product rows stay in its own track. |
| **component registry** (TryAdd-skip / Replace-displacement ledger) | feature | Quality | Idea 2026-07-19 → **build narrower**. 175 `Add*` · 184 `TryAdd` sites; a skipped `TryAdd` leaves *no trace anywhere*. The BCL already dumps the positive space (`DependencyInjectionEventSource` event 8); only the **negative space** is unsolved — and it's the class D5/D7 belong to. See deep-dive. |
| Roslyn analyzer: foundation-can't-import-domain | check | Quality | Decision 9.14 open. |
| `apps/playground/` Aspire end-to-end smoke | check | Quality | Nothing validates wrappers end-to-end today. |
| Haven.Auth dogfood migration onto shipped OTP/issuance/policies | check | Identity | Validates the seams before products depend on them. |

## Deep-dives

| Feature | Doc | Status |
|---|---|---|
| Identity (own, sliced) | [identity/identity-architecture.md](identity/identity-architecture.md) | drafted |
| Data session | [data-pipeline/data-session-architecture.md](data-pipeline/data-session-architecture.md) | designed + adversarially verified · [test plan](data-pipeline/data-session-test-plan.md) · [verdict](data-pipeline/data-pipeline-verdict.md) · [research 01–05](data-pipeline/research/) |
| Component registry | [component-registry/component-registry-idea.md](component-registry/component-registry-idea.md) | researched · verdict = build narrower |
