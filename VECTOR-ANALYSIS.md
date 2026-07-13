# Backend SDK — Vector Analysis

*Last updated: 2026-07-13*

> **Purpose.** A ground-truth map of every capability **vector** in `WoW.Two.Sdk.Backend.Beta`: what exists, how mature each component is, what to **improve** (existing but incomplete) vs **establish** (scaffold or entirely absent). Feeds the "choose and implement" decision that follows.
>
> **Method.** Six parallel analysis passes read the **actual `src/` code** (not the docs) on 2026-07-13 at `10.0.50-beta`. Where the shipped docs (`targets.md`, `README.md`, `CLAUDE.md`, `package-registry.md`) disagree with the code, **the code wins** and the drift is logged in Appendix A. Maturity is 0–5: **0** Scaffold (folders, no `.cs`) · **1** Skeleton (interfaces/options only) · **2** Partial · **3** Functional (main path) · **4** Solid · **5** Complete/saturated.
>
> **Vector vs component.** A *vector* is an orthogonal capability domain (identity, data, messaging). Each vector has *components* — e.g. identity = {user-model, providers, token-generation, token-storage, authentication, authorization}. We rate at both levels: the vector score is the composite; the component table is where the real work-list lives.

---

## 1. Scoreboard — all vectors

| Vector | Mat. | Bucket | One-line state |
|---|:--:|---|---|
| **Foundation** | 4 | improve | Base layer; error/result model **unified** (5), security + audit (5); missing `Result` combinators, async validation |
| **Web** | 4 | improve | Exception→ProblemDetails→hosting triad solid; one **dead `Results/` folder**; OpenAPI thin (no UI/security schemes) |
| **Http** | 4 | improve | Outbound near-complete (Refit, Polly-v8 resilience, hedging, OAuth2-CC, mTLS); no static-bearer handler, no typed `HttpClientError` |
| **Mediator** | 4–5 | improve | MediatR-free facade; **result-absorption resolved** (`AppResult<T>`); all 5 behaviors real; docs stale; streaming a non-goal |
| **Messaging** | 4 | improve | Real bus + 3 live brokers + outbox/inbox/saga/webhooks; **saga not durable**, 7 scaffold brokers, no metrics/health |
| **Data** | 4 | improve | Contracts/audit/soft-delete/**migrator (5, standout)** solid; PG floor **drops pooling+retry**; no UoW, no bulk, Postgres-biased |
| **Integrations** | 4 | improve | GitHub + GHCR read-only deploy probes over `IAccessTokenProvider`; product-pulled (drydock) |
| **Meta** | 4 | improve | P1 boot floor composes foundation+obs+web; **does not fold** caching/http/messaging/ai/flags; web-only (no worker entrypoint) |
| **Testing** (×3) | 4 | improve | Real host + 6 container fixtures + migrator/EF harness; **Verify `traceId` scrubber is a no-op bug**; no SqlServer fixture |
| **Observability** | 3 | improve | Tracing/metrics/logging solid; **health-checks = bare passthrough**; **logs not bridged to OTLP**; no redaction |
| **Codes** | 3 | improve | QR render solid, barcode half-built; a **domain** vector in an infra SDK — home is questionable |
| **Identity** | **2** | **improve (flagship)** | Broad, solid primitives on a **1/10-built own-identity spine**; no refresh/revocation, no sign-in orchestration, external logins don't persist, **two parallel identity systems** |
| **Jobs** | 2 | improve | Hangfire-only, **no provider-neutral port** (consumers bind Hangfire types directly); 5 empty adapter folders |
| **Comms** | 2 | improve | **Email-only** (MailKit/SendGrid/SES); SMS + Push + templating + unified `Core` all scaffold |
| **Media** | 2 | improve | VTT captions only (own parser, solid); audio + transcripts LATER |
| **Caching** | **0** | **establish** | Scaffold (7 folders, 0 `.cs`); HybridCache + Redis-L2 plan ready |
| **Tenancy** | **0** | **establish** | Scaffold; `IHasTenant<T>` hook already placed in Data; mirror the soft-delete filter |
| **Ai** | **0** | **establish** | Scaffold (13 provider folders, 0 `.cs`); `IChatClient` seam first; AI-forward SDK ⇒ high pull |
| **FeatureFlags** | **0** | **establish** | Scaffold (7 folders); MS FeatureManagement + OpenFeature seam |
| **Realtime · Storage · Search · Documents · Workflow · Payments · Geo · Localization · Secrets · gRPC · GraphQL · OData** | — | **establish from zero** | **No folder exists.** Ranked in §4 |

**Distribution:** 11 vectors at 3–5 (a genuinely strong boot-floor + pipeline + persistence spine) · 5 at 2 (broad-but-shallow: Identity, Jobs, Comms, Media, +Codes) · 4 scaffold · 12 absent.

---

## 2. IMPROVE — existing vectors, per-component work-lists

### 2.1 Identity — **2/5** (the flagship gap)

The user's canonical example, and the largest improve opportunity: 84 `.cs`, but the maturity is bimodal — **primitives are 3–4, the own-identity spine is 1–2.** The 2025 "own sliced-lego identity" rebuild is **step 1 of 10**: user model + schema exist, but almost nothing composes them into an end-to-end product path.

| Component (user's model) | Mat. | State |
|---|:--:|---|
| user-model / store | 2 | `IdentityUser`/`IdentityRole` + 5 relation entities + `ApplyIdentitySchema` (7 tables) exist and are Data-integrated; but `IUserStore` is **CRUD-only** (6 methods) and **0 of ~11 optional slices** exist (password/email/login/role/token/security-stamp/lockout/2FA stores) |
| providers (OAuth/OIDC) | **4** | **Near-complete** — 16 uniform `Add{Provider}Authentication` + generic OIDC + Google-ID-token verifier + 17-provider `wt:*` claim normalizer. The differentiated asset |
| token-generation | 3 | `AddJwtTokenIssuance` mints HMAC JWTs (ASP.NET-free); symmetric-only, no key rotation, caller hand-builds claims |
| token-storage (refresh/revoke) | **1** | **Effectively absent** — issuance doc says "no refresh, no revocation"; `identity_user_tokens` table exists but has no store/provider. Refresh only via the ASP.NET `MapIdentityApi` escape hatch |
| authentication | 3 | Strong primitives (JWT bearer+JWKS, cookie SPA/MVC modes, OTP, TOTP, WebAuthn, Argon2id, guest); **no `ISignInService`** — every app hand-glues password→lockout→2FA→token |
| authorization | 3 | Role-in-scope + allowlist + default-deny gates (only area with `standard.md`+`spec.md`); **no permission/scope/ABAC**, roles not wired to the role entities |

**Structural finding — two non-integrated identity systems coexist:** the new sliced `Core` (`AddUserAccounts<TUser>`) vs the old ASP.NET wrapper `IdentityApi` (`AddIdentityApiEndpoints` → `MapIdentityApi`). They use **incompatible DbContext bases** — a product must pick the mature-but-rejected god-object or the pure-but-1/10 own core; they can't mix. Argon2 hashing currently targets the **old** system's `IPasswordHasher`, orphaned from the new core.

**Top improvements (ranked):**
1. **Build the sliced stores on the existing schema** — `IUserPasswordStore` (rewire Argon2 to the new core), `IUserLoginStore` (persist the 17 OAuth logins that currently evaporate), `IUserRoleStore`/`IRoleStore`. Turns the schema from decorative into functional.
2. **Refresh + revocation slice** — the #1 product blocker. A refresh-token store (table already mapped) + `IUserSecurityStampStore` + stamp-validation middleware delivers refresh *and* revocation together.
3. **`ISignInService`** — the missing orchestrator; without it every consumer re-implements login.
4. Asymmetric issuance (RS256/ES256) + `IKeyProvider` rotation seam.
5. Persistent OTP store companion (`MemoryOtpStore` breaks on scale-out) + 2FA/WebAuthn credential persistence.
6. **Resolve the two-systems tension** — retire `IdentityApi` once sliced sign-in lands, or explicitly document it as the interim turnkey.

---

### 2.2 Messaging — **4/5**

Far past its stale docs (`README`/`CLAUDE` still say "planned"). Real transport-agnostic bus, 3 live brokers, transactional outbox/inbox, routing-slip saga, signed webhooks.

| Component | Mat. | State |
|---|:--:|---|
| bus / dispatcher | 4 | `TransportEventBus` + ordered `IConsumeFilter` pipeline; DI-scope-per-event, correlation auto-propagation |
| transports | 4 / 0 | **Real:** InMemory, RabbitMQ (native DLX), Kafka (emulated DLQ), NATS JetStream. **Scaffold (0 `.cs`):** AzureServiceBus, AzureEventHubs, AwsSqs, Cap(+6), Mqtt |
| outbox | 4 | `EfOutbox` (no dual-write) + `OutboxDispatcher` + `PostgresSkipLockedOutboxClaimStrategy` (multi-instance) |
| inbox | 3 | Real exactly-once; keyed by `message_id` only (breaks a shared inbox DB — needs `(consumer_id, message_id)`) |
| saga | **3** | Routing-slip + reverse-order compensation + `ToMermaid()`; **in-memory only — dies on crash** |
| webhooks | 3 | HMAC-SHA256 + SSRF guard + retry; outbound-only, in-memory subs, **no DLQ** |
| reliability | 4 | Retry (exp+jitter), dead-letter store w/ redrive, Polly swap-in |
| serialization | 3 | STJ + stable-type-token registry; outbox+webhooks still bypass it on direct STJ |

**Top improvements:** (1) **saga durability** (`ISagaRepository<TState>` — the headline feature is memory-only); (2) per-consumer inbox scoping; (3) **`IMessagingMetrics` + per-transport health checks** (zero today); (4) **Azure Service Bus adapter** (highest-value next broker — native DLQ/sessions/dedupe); (5) webhook DLQ + persistence; (6) Wave-2 correctness (RabbitMQ auto-recovery, publisher confirms).

---

### 2.3 Data — **4/5**

| Component | Mat. | State |
|---|:--:|---|
| repository | 4 | `IReadRepository`/`IRepository` + EF & Dapper impls; no spec/query surface (deliberate), no pagination |
| unit-of-work | **1** | **No `IUnitOfWork`** — every repo write auto-`SaveChanges`; multi-aggregate atomicity is hand-rolled |
| ef-core-config | 4 | `AddEntityFrameworkCore<T>` — pooling on, interceptor auto-wire |
| audit | 4 | `AuditInterceptor` (CreatedAt/By, UpdatedAt/By via `TimeProvider`+accessor) |
| soft-delete | **5** | Interceptor + global query filter, owned-type correct |
| naming | 4 | snake_case + reversible enum converter over `Foundation.Naming` |
| migrations-runner | **5** | Bespoke SQL migrator — advisory-lock, SHA-256 drift, orphan fail-closed, `wow-migrate` CLI; green in 3 apps. **Standout** |
| dapper | 4 | conventions + type handlers + CRUD repo |
| multi-provider | 2–3 | **Postgres-only** full floor; SqlServer/Sqlite/Cosmos are thin builder presets |
| bulk | 0–1 | No `EFCore.BulkExtensions`; `CreateRangeAsync` is change-tracked, not bulk-copy |
| errors-mapping | 3 | `AppError` mapping is **Postgres-only** (SqlServer/Sqlite fall through to `Unexpected`) |

**Top improvements:** (1) **`AddPostgresPersistence` silently drops two LOCKED defaults** — no DbContext pooling, no `EnableRetryOnFailure`/`CommandTimeout` (both exist elsewhere but the flagship one-call composition bypasses them) — **bug-class, high-value, small fix**; (2) provider-floor parity (`AddSqlServerPersistence`/`AddSqlitePersistence`); (3) `IUnitOfWork` seam; (4) bulk module; (5) broaden error mapping past Postgres.

---

### 2.4 Foundation — **4/5** (core is 5)

**The two-model error problem is RESOLVED and shipped green (~198 tests).** One `AppError` value model (`ValidationError`/`AppAggregateError` as subtypes); the old `DomainError` (HTTP-int-baked) and per-product `FailureCategory`/`ICategorizedFailure` are **deleted**. Two result *carriers* by design (`Result<T>` everywhere, `AppResult<T>` at the mediator↔controller seam) both carry the same `AppError`. **MVC ProblemDetails gap CLOSED** — `ValidationExceptionFilter` (`IExceptionFilter`) now exists alongside the pipeline handler.

- **5/5:** results, errors, security (AES-256-GCM envelope crypto, KEK/DEK), audit (SHA-256 hash-chain tamper-evidence)
- **4/5:** validation (FV hidden behind `IValidator<T>` adapter; sync-only), naming
- **3/5:** time, guards (only 2 custom), serialization (presets only), configuration
- **Improvements:** result combinators (`Bind`/`Tap`/`Ensure`), `IAsyncValidator<T>`, i18n hook; **fix layer bleed** — `IErrorMessageResolver` in Foundation depends on `HttpContext` (transport leak into the transport-free base).

---

### 2.5 Web / Http / Meta / Observability / Mediator / Codes / Testing / Jobs / Comms / Media / Integrations — condensed

- **Web (4):** ExceptionHandling **5**, ErrorMapping/ProblemDetails/Hosting/Cors/SecureHeaders/RateLimit/Compression/Versioning/Json all **4**, OpenApi/OutputCache/Contracts **3**, **`Results/` = dead folder (0 `.cs`, doc describes deleted types)**. Improve: delete dead folder+doc, OpenAPI enrichment (security schemes + Swagger/Scalar UI), wire MVC validation filter into `AddApiDefaults`.
- **Http (4):** resilience **5**, refit/core/hedging/auth-handlers/header-propagation **4**. Establish a typed `HttpClientError` model (the one acknowledged hole) + a plain static-bearer/API-key handler.
- **Meta (4):** composes foundation+observability(5 of 8)+web(9). **Doesn't fold** caching/http/messaging/jobs/comms/tenancy/ai/flags/media/integrations; **`AppErrorObserver` error seam not wired**; **web-only** (no worker/background-host entrypoint). Improve: wire error observer, add worker floor, fold newer vectors as they mature.
- **Observability (3):** tracing/metrics/logging **4**; **health-checks = 1** (bare `AddHealthChecks()` passthrough, zero Xabaril providers); **logs not bridged to OTLP** (breaks the LOCKED Trace+Metrics+**Logs** triad; OTLP XML-doc even claims logs it doesn't wire); **redaction = 0**. Improve: real health-check providers, log→OTLP bridge, Serilog masking policy.
- **Mediator (4–5):** core dispatch **5**, result-absorption **5** (exceeds its planning doc), 5 behaviors **4–5**. Improve: sync stale docs (`Send`→`SendAsync`, 2-generic→1-generic `AppResult`); parallel-publish for notifications; dogfood into products (delete their `Common/Mediator` copies).
- **Testing ×3 (4):** real `WebApiTestHost` + 6 container fixtures + Respawn + `RelationalTestDb` provider-switch + migrator harness. **Bug: `VerifyDefaults` `traceId` scrubber replaces a string with itself (no-op) → snapshot instability.** Add SqlServer + Elasticsearch/LocalStack fixtures; BenchmarkDotNet perf harness.
- **Codes (3):** QR render **4** (styled SVG + logo/emoji knockout + ECC auto-floor), barcode **2** (ZXing SVG, style ignored, no PNG). **Decision needed:** does a QR/barcode *domain* engine belong in an *infra* SDK, or in a dedicated `wow-two-sdk` domain package?
- **Jobs (2):** Hangfire wrapper works but **no `IJobScheduler` port** — "Jobs" ≈ "Hangfire"; Coravel/NCronJob/SqlServer/Redis empty. Establish the provider-neutral port first.
- **Comms (2):** Email **3** (3 providers); **SMS 0, Push 0, templating 0, `Core` 0**. Establish SMS (Twilio) + Push (FCM/APNS) + FluentEmail templating + a unified channel port.
- **Media (2):** Captions **~3–4** (solid own VTT parser w/ YouTube overlay dedup); vector is single-format. Improve: emit `CaptionTrack`; SRT/TTML deferred until a 2nd format is real.
- **Integrations (4):** GitHub + GHCR **read-only** probes; solid but narrow. Improve: write-side (workflow-dispatch, create-release), pagination/ETag; add to `targets.md` (currently untracked).

---

## 3. ESTABLISH — scaffold vectors (folders exist, 0 impl)

Build order within each is seam-first (the abstraction before any adapter).

### 3.1 Caching — 0 → target 4 · **P3, broadly needed, no blockers**
`Hybrid/` (`AddHybridCache` default, L1+L2+tags+stampede) → `Core/` (`ICache`/`GetOrSetAsync<T>` cache-aside facade) → `Redis/` (L2) → `Memory/` (L1) → tag invalidation → per-tenant key prefix (Tenancy dep) → ETag/OutputCache bridge (Web). FusionCache/SqlServer/Cosmos are LATER. Decision 9.4 (HybridCache vs FusionCache) is open — HybridCache is first-party, default.

### 3.2 Ai — 0 → target 3–4 · **AI-forward SDK, transcript-forge already pulling**
`Core/` `IChatClient` + `IEmbeddingGenerator` seam (`Microsoft.Extensions.AI`, LOCKED) → provider wave 1 (OpenAI, AzureOpenAI, Anthropic, Ollama) → `Tokenizers` + **cost/token OTel meter** (Observability dep) → `Vector/*` stores (**pgvector already wired in-tree**) → SemanticKernel/Mcp → embedding+response cache (Caching dep). Seam before any adapter.

### 3.3 Tenancy — 0 → target 3 · **SaaS-shaped, deps satisfiable**
`Core/` (`ITenantContext`/`ITenantResolver` + subdomain/header/route/JWT-claim resolvers) → **`PerRow/`** (EF global query filter on the **already-placed `IHasTenant<T>`** — a direct clone of the existing soft-delete filter machinery, lowest-effort/highest-value) → `Finbuckle/` wrap → `PerDb/` connection routing → tenant store → per-tenant cache/config overlay. Cross-deps: Data ✓, Identity ✓, Caching (unbuilt).

### 3.4 FeatureFlags — 0 → target 3 · **P5, small**
`Core/` (`Microsoft.FeatureManagement` + `.AspNetCore`, `[FeatureGate]`) → `OpenFeature/` seam → vendor adapters (ConfigCat/LaunchDarkly/GrowthBook/Unleash/Esquio) as OpenFeature providers → variant/experiment tracking.

---

## 4. ESTABLISH FROM ZERO — missing vectors (no folder)

All 12 confirmed genuinely absent (no `src/` folder, no central-package wiring). Ranked by product-value × dependency-readiness. **The doctrine payoff:** the top vectors ride dependencies the SDK *already ships* — the integration cost is largely pre-paid.

| # | Vector | Value | Dep-readiness | Home | Why now |
|:--:|---|:--:|:--:|---|---|
| 1 | **Realtime** (SignalR + SSE) | High | High (Web+Redis+Ai ✓) | core | **LLM token-streaming is an immediate pull** — `Ai/` is shipping; SSE is cheap |
| 2 | **Storage / blob** (FluentStorage) | High | High (Foundation/Web ✓) | core | Most broadly-needed absent SaaS primitive (avatars, uploads, exports) |
| 3 | **Search** (PG tsvector + pgvector) | Med-High | **Very High** — pgvector already in-tree | core | Readiest deps of all; pairs with shipped `Ai/Vector` |
| 4 | **Documents** (QuestPDF/ClosedXML/CsvHelper) | Med-High | High (libs chosen) | core | Invoices/exports/reports; pairs with #2 |
| 5 | **Workflow** (Stateless) | Med | High (Messaging ✓) | core | Tiny MIT lib, low integration cost; order/approval/onboarding machines |
| 6 | **Payments** (Stripe) | High-niche | High (Webhooks ✓) | companion | Webhook sig-verify infra exists; commerce-domain ⇒ companion |
| 7 | Geo / IP (NTS + MaxMind) | Low-Med | High (Data ✓) | core | Niche pull |
| 8 | Localization / i18n | Low-Med | High (first-party) | core | Plan defers till demand |
| 9 | Secrets mgmt (Vault/ASM) | Med (ops) | Med (Security seam ✓) | companion | Env vars suffice early-stage |
| 10 | gRPC | Low-Med | High (Web ✓) | pending §9.10 | Low REST-SaaS pull |
| 11 | GraphQL (HotChocolate) | Med | Med | companion | Heavy surface by design |
| 12 | OData | Low | High | companion | Niche |

---

## 5. Cross-cutting findings

### 5.1 Security batch (spans Identity + Http + Web)
Not one vector — a themed set of hardening slices, several folded into the Identity rebuild: **token revocation + refresh** (Identity, absent), JWT validation hardening (alg allowlist, mandatory iss/aud/exp), **SSRF-safe outbound handler** (webhooks have it; general HttpClient doesn't), CSRF/antiforgery preset for cookie flows, account lockout counting, breached-password (HIBP k-anonymity). Request-limits + host-filtering + HTTPS-redirect + OpenAPI-env-gate already shipped (2026-07-10).

### 5.2 Meta doesn't keep pace with the vectors
The boot floor composes P1 only. As Caching/Http/Messaging mature they stay un-folded, so the "one import" promise erodes for anything past the floor. Wire matured vectors in behind opt-out flags; add a **worker/background-host** entrypoint (today `AddApiDefaults` is `WebApplicationBuilder`-only).

### 5.3 The audit/soft-delete interceptor is the reusable template
`SaveChangesInterceptor` + `AddEfInterceptor<T>` auto-wire is the proven pattern for the two biggest unbuilt data-adjacent pieces: **tenant-stamping** (Tenancy `PerRow`) and **cache-invalidation-on-save** (Caching). Don't invent new plumbing.

### 5.4 Provider bias = Postgres everywhere
Data floor, migrator dialects, and error mapping are Postgres-first (SQLite second, SqlServer/Cosmos thin). Any per-tenant-per-DB or cache-provider work inherits this; SqlServer parity is a recurring latent cost.

### 5.5 Bugs found (small, real)
1. `AddPostgresPersistence` drops pooling + `EnableRetryOnFailure` (LOCKED defaults) — §2.3.
2. `VerifyDefaults` `traceId` scrubber is a self-replace no-op → snapshot instability — §2.5 Testing.
3. `IErrorMessageResolver` (Foundation) depends on `HttpContext` — transport bleed into the transport-free layer — §2.4.

---

## 6. Recommended sequencing (for "choose and implement")

Three tracks, roughly in value order. Pick from here.

### Track A — Quick wins (small, high-leverage, mostly bug-class)
1. Fix `AddPostgresPersistence` → route through pooling + retry defaults.
2. Fix the `VerifyDefaults` `traceId` scrubber no-op.
3. Wire `AddAppErrorObserver()` into the Meta floor (opt-out).
4. Observability: wrap Xabaril health-check providers (PG/Redis/RabbitMQ/…) + liveness/readiness split.
5. Doc-drift cleanup (Appendix A) — delete `Web/Results/results.md`, refresh the 4 stale planning/mediator docs.

### Track B — Deepen the flagship (Identity, 2 → 4)
The highest-value *improve*. Order: sliced stores (password/login/role) → **refresh + revocation** → `ISignInService` → resolve the two-systems tension. Each is one slice; together they make "own identity" a real product path and retire the ASP.NET fallback.

### Track C — Establish the next capability (pick 1–2)
- **Caching** (scaffold → 4): no blockers, broadly needed, unblocks per-tenant + AI response cache.
- **Realtime** or **Ai**: both AI-forward, deps pre-paid. Ai `Core` seam + Realtime SSE together unlock LLM streaming end-to-end — the clearest product-pulled pair.
- **Tenancy `PerRow`**: cheapest SaaS unlock (clone the soft-delete filter onto the already-placed `IHasTenant<T>`).

**Suggested first cut:** Track A in full (a day of cleanup that removes real defects + drift), then **Identity refresh/revocation + stores** (Track B) as the flagship deepening, with **Caching** (Track C) as the parallel establish. Confirm before implementing.

---

## Appendix A — Doc drift log (code is truth)

| Doc | Claims | Reality |
|---|---|---|
| `README.md` / `CLAUDE.md` | "messaging + webhooks planned" | Both **shipped** (bus + 3 brokers + outbox/saga/webhooks) |
| `targets.md` §2.9 | Caching "DONE / LOCKED" | **0 `.cs`** — scaffold (phase table's "deferred" is honest) |
| `docs/analysis/validation-and-result-pattern.md` | `DomainError`, `ValidationResult` DU | Superseded — unified `AppError` model shipped |
| `mediator.md` / `mediator-cqrs-result-absorption.md` | `Send`, 2-generic `AppResult<T,F>` | Now `SendAsync`, 1-generic `AppResult<T>` + `AppError` |
| `Data/Migrations/migrations.md` | folder `Sql/` | On disk it's `Bespoke/` |
| `Web/Results/results.md` | `FailureCategory`/`ICategorizedFailure` | Types **deleted**; folder has no `.cs` |
| `Observability/Otlp` XML doc | exports "traces, metrics, and logs" | Logs **not** wired |
| `package-registry.md` | Data rows = "scaffold" | Data is Functional/Solid (registry stale vs 2026-07 work) |

## Appendix B — Evidence base
Six parallel ground-truth passes (2026-07-13, `10.0.50-beta`), each reading actual `src/` code: Identity · Data/Caching/Tenancy · Messaging/Mediator/Jobs/Comms · Web/Http/Foundation/Codes · Observability/Ai/FeatureFlags/Media/Integrations/Meta · Testing + missing-vector scan. Raw per-vector component tables and file citations available on request.
