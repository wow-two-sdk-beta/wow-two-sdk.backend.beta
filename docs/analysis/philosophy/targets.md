# Backend Implementation Targets — what we'll build

*Last updated: 2026-05-04*

> Companion: [`ideas.md`](./ideas.md) — every .NET tech / pattern / library / runtime API that exists. **No verdicts.**
>
> **This file**: verdict per item. Mirrors `ideas.md`'s structure. Read when deciding what to ship next.
>
> **Target package**: `wow-two-sdk.backend.beta` — single big NuGet bundling everything below. Dependency bloat is *intentional*; one-import is the win.

---

## 0. Verdict legend

| Verdict | Meaning |
|---|---|
| **LOCKED** | Architectural decision. Won't change without proposal. |
| **DONE** | Already shipped (or imported as-is). |
| **NOW** | Active sprint. Implement before next batch. |
| **NEXT** | Plan to start once NOW clears. |
| **LATER** | On the roadmap, not soon. Keep visible so it doesn't drift. |
| **MAYBE** | Unclear — needs triage / spike before commit. |
| **SKIP** | Decided not to implement. Reason given. |

---

## 1. Locked architectural decisions

| # | Decision | LOCKED rationale |
|---|---|---|
| 1.1 | **Single big package** — `wow-two-sdk.backend.beta` aggregates every concern; subpath imports per area | Dependency bloat is explicitly accepted. One install, batteries included. Mirrors UI lib's "one big package, subpath exports". |
| 1.2 | **.NET 9 baseline, multi-target later if needed** | Native AOT, HybridCache, first-party OpenAPI, `Microsoft.Extensions.AI` are .NET 9. Don't carry old-runtime debt. |
| 1.3 | **C# 13+ language features freely** | Primary constructors, collection expressions, partial properties, etc. No `LangVersion` floor pinning. |
| 1.4 | **Built-in DI (`Microsoft.Extensions.DependencyInjection`) is the contract** | Don't ship Autofac/Lamar replacements. Consumers can swap if they want — we expose `IServiceCollection` extensions. |
| 1.5 | **Source-gen preferred over reflection** for mapping, JSON, validation, DI graph, regex | AOT-friendly, faster, fewer trim warnings. |
| 1.6 | **AOT-compatible best-effort** | Don't *require* AOT, but every component should compile under `<PublishAot>true</PublishAot>` without warnings if possible. |
| 1.7 | **Modular Monolith first** — microservices is a deployment choice, not a code shape | Make module boundaries cheap (DI conventions, in-process mediator, Outbox abstractions). Easy escalation path to distributed messaging later. |
| 1.8 | **Clean Architecture × Vertical Slice hybrid** | Layering for the foundation (`tokens`, `primitives`, `domains`); vertical slices inside each domain feature. Borrow the UI lib's `foundation/domain` mental model. |
| 1.9 | **Beta-forever versioning** | `0.x.y`, CI auto-bumps `y` on every merge to `main`. Fix-forward when broken. No CHANGELOG, no PR gates, no required tests. Mirrors UI lib's beta-forever rule. |
| 1.10 | **System.Text.Json default; Newtonsoft only when an external lib forces it** | STJ is now feature-complete enough (polymorphism, source-gen, JsonNode mutability). Newtonsoft pulled in transitively only. |
| 1.11 | **`Microsoft.Extensions.Logging.ILogger<T>` is the only public log surface** | Internally we may use Serilog, but consumers see `ILogger<T>`. Same pattern for telemetry — `ActivitySource` and `Meter` are the public seams. |
| 1.12 | **No commercial license deps in the core meta-package** | MediatR (commercial 12.0+), AutoMapper (commercial 14.0+), MassTransit (commercial v9+ announced), Duende.IdentityServer (RPL), iText7 (AGPL), Aspose, Spire — all are SKIP for the core. May surface as opt-in adapters in companion packages but never as core dependencies. |
| 1.13 | **Permissive licenses only in core**: MIT, Apache-2.0, BSD-3, BSD-2, MS-PL, Unlicense | LGPL only when it's truly transitive-and-isolated (e.g. linking decisions). AGPL never. |
| 1.14 | **No PR gates, no required tests, push to main** | Beta-forever rule, mirrors UI lib. |
| 1.15 | **Standard + spec before code (per-component)** | Borrow the UI lib pattern — every public abstraction begins as `*.standard.md` (RFC 2119 contract) and `*.spec.md` (concrete API). |
| 1.16 | **Subpath exports per top-level src/ folder** | Consumers can pull `wow-two-sdk.backend.beta.web` only, or pull the meta. Modeled on UI lib's `forms/`, `display/`, `nav/` subpaths. |

---

## 2. Cross-cutting vector verdicts

Mirrors §4 of `ideas.md`. Each subsection states the verdict + concrete chosen library/abstraction + rationale.

### 2.1 Dependency Injection — DONE (foundation)

| Item | Verdict | Note |
|---|---|---|
| Built-in `Microsoft.Extensions.DependencyInjection` as the contract | DONE / LOCKED | Public seam |
| Keyed services (.NET 8+) | DONE | Use freely |
| Scrutor for assembly scanning + decorators | NOW | Add as core dep |
| Open generic decorators (e.g. `IRepository<>` decorator chain) | NOW | Convention helpers |
| Source-gen DI (Pure.DI / Jab / StrongInject) | SKIP | Not switchable; reflection-based MS DI is the floor |
| Autofac / Lamar / DryIoc / SimpleInjector swap | SKIP | Consumer's problem |
| `IHostApplicationLifetime` lifecycle hooks helper | NEXT | Wrap with safer ergonomics |

### 2.2 Configuration & Options — NOW (P1)

| Item | Verdict | Note |
|---|---|---|
| JSON / Env / CLI / UserSecrets sources | DONE | Inherit from MS defaults |
| YAML provider via `NetEscapades.Configuration.Yaml` | NOW | Add to core, opt-in |
| Azure App Configuration adapter | NEXT | Optional; gated behind `Aspire.*` companion |
| Vault / AWS Secrets Manager / GCP Secret Manager | LATER | Companion packages |
| Options validation: source-gen `[OptionsValidator]` + FluentValidation bridge | NOW | Both — `[OptionsValidator]` for AOT, FV for richer rules |
| `ValidateOnStart()` everywhere by convention | NOW | Fail fast at boot |
| Hot reload via `IOptionsMonitor<T>` | DONE | Inherent |
| Strongly-typed `record` config types | LOCKED | Prefer `record TFooOptions(...)` with init-only |

### 2.3 Logging — NOW (P1)

| Item | Verdict | Note |
|---|---|---|
| `ILogger<T>` is public seam | LOCKED | Don't expose Serilog types |
| Serilog under the hood by default | NOW | Console + File + Seq sinks pre-wired |
| `[LoggerMessage]` source-gen for high-perf log methods | LOCKED | Use everywhere internally |
| Compliance redaction (`Microsoft.Extensions.Compliance.{Classification, Redaction}`) | NEXT | Tag DTOs with `[DataClassification]` |
| Correlation ID + trace ID enrichment by default | NOW | Via `LogContext`/`Activity` |
| OTel structured-log exporter wired by default | NOW | OTLP exporter |
| ZLogger (Cysharp) | MAYBE | Spike against Serilog; likely SKIP for ergonomics |
| NLog / log4net | SKIP | Pick one path; Serilog wins |

### 2.4 Validation — DONE (FluentValidation) + NEXT for ProblemDetails wire-up

| Item | Verdict | Note |
|---|---|---|
| FluentValidation as the rule engine | LOCKED | Industry std; permissive license |
| DataAnnotations for primitive constraints (Options, model binding) | DONE | First-party |
| Vogen for value-object invariants | NEXT | Source-gen, AOT-safe |
| Ardalis.GuardClauses for argument guards | DONE | Add to core |
| Hellang.Middleware.ProblemDetails for FV → 400 | NOW | Default registration |
| Async validators (DB lookup) | DONE | Use FV's async surface |
| ICU MessageFormat for plurals | LATER | i18n companion |
| MiniValidation as fallback for AOT-strict scenarios | MAYBE | Only if FV trim becomes a problem |

### 2.5 Mapping — NOW (Mapperly default)

| Item | Verdict | Note |
|---|---|---|
| **Riok.Mapperly** as default — source-gen, AOT-safe | LOCKED | First-class wrapping helpers |
| Manual hand-written mappers when natural | DONE | Idiom; encourage in slice code |
| Mapster as alt | MAYBE | Only if Mapperly hits a generation wall |
| AutoMapper | SKIP | Commercial 14.0+; reflection-based; AOT-hostile |
| AgileMapper / ExpressMapper / EmitMapper / TinyMapper | SKIP | Stale or niche |

### 2.6 Resilience — NOW (P1)

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.Extensions.Http.Resilience` (Polly v8) standard handlers | LOCKED | Default for outbound HttpClient |
| `Microsoft.Extensions.Resilience` for non-HTTP pipelines | NOW | Generic resilience pipelines |
| Standard-Hedging handler | NEXT | Per-route opt-in |
| Polly Chaos / Simmy fault injection | LATER | Test-only |
| Retry budgets, idempotency keys | NEXT | First-class concern |
| Circuit breaker state in Redis (multi-instance) | LATER | Custom strategy |
| Steeltoe Hystrix port | SKIP | Displaced by Polly v8 |

### 2.7 Observability — NOW (P1)

| Item | Verdict | Note |
|---|---|---|
| OpenTelemetry .NET (Trace + Metrics + Logs) | LOCKED | Default everywhere |
| OTLP exporter as default | LOCKED | Aspire-friendly |
| Auto-instrumentation: AspNetCore + Http + EFCore + StackExchangeRedis + GrpcNetClient + Runtime + Process | NOW | Core registration helpers |
| Aspire ServiceDefaults pattern shipped as a project template | NEXT | Optional |
| `ActivitySource` named per module | LOCKED | Convention: `WoW.Two.{Module}` |
| `Meter` named per module | LOCKED | Same convention |
| Health checks via Xabaril `AspNetCore.HealthChecks.*` | NOW | Pre-wired for SQL Server, Postgres, Redis, RabbitMQ, ASB, Mongo, Elasticsearch, Network |
| Application Insights, Datadog, NewRelic, Sentry | LATER | Companion adapters |
| dotnet-monitor sidecar profile | LATER | Reference Dockerfile only |

### 2.8 Security & Auth — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| JWT bearer + cookie + OIDC schemes | DONE | First-party; lib wires sane defaults |
| ASP.NET Core Identity + Identity API endpoints | DONE | First-party |
| Microsoft.Identity.Web for Entra ID | NEXT | Companion adapter |
| OpenIddict for OSS OIDC server | NEXT | Optional adapter (Apache-2.0) |
| Duende IdentityServer | SKIP | Commercial license; not in core |
| WebAuthn/FIDO2 via Fido2.AspNetCore | LATER | Passkeys |
| Otp.NET for TOTP | NEXT | 2FA foundation |
| ABAC / Permission-based via custom `IAuthorizationHandler` | NEXT | Convention helpers |
| OPA / Casbin / SpiceDB integration | LATER | Heavy clients |
| BFF pattern via Microsoft.Identity.Web.Bff | NEXT | OSS path |
| Duende.BFF | SKIP | Commercial |
| Anti-forgery + DataProtection + KeyVault key wrapping | NOW | Sane defaults |
| Argon2 password hashing via Konscious.Security.Cryptography | NEXT | Replace PBKDF2 default |
| Post-quantum (`MLKem`/`MLDsa`) | LATER | Wait for .NET 10 GA |
| OWASP secure-headers middleware (CSP, HSTS, X-Content-Type, etc.) | NOW | Opt-in pre-built |

### 2.9 Caching — DONE (HybridCache as default)

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.Extensions.Caching.Hybrid` as default | LOCKED | .NET 9 — L1+L2 + tags + stampede |
| StackExchange.Redis as L2 backend | DONE | Pre-wired |
| FusionCache as alt for richer scenarios | MAYBE | Spike vs HybridCache; only if HybridCache lacks fail-safe / soft-timeout we need |
| Memory cache (`IMemoryCache`) for trivial cases | DONE | Inherit |
| OutputCaching middleware | NOW | Default opt-in for read-heavy endpoints |
| ResponseCaching | SKIP | OutputCaching supersedes |
| EF Core L2 (EFCore.SecondLevelCache.Core) | LATER | Per-app opt-in |
| CDN / browser cache header conventions | NOW | ETag + Cache-Control helpers |

### 2.10 Rate limiting — NOW (P1)

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.AspNetCore.RateLimiting` middleware | LOCKED | Built-in |
| Per-IP / per-user / per-tenant / per-route policies | NOW | Convention helpers |
| Distributed rate limiter via Redis | NEXT | Custom `PartitionedRateLimiter<T>` |
| AspNetCoreRateLimit | SKIP | Pre-built-in API; superseded |
| 429 + `Retry-After` + RFC-compliant ProblemDetails | NOW | Default |

### 2.11 Background jobs — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| `BackgroundService` / `IHostedService` patterns | DONE | First-party |
| **Hangfire** with SQL Server / Postgres / Redis storage | NEXT | Default for distributed jobs |
| Coravel for in-process scheduling | MAYBE | Lighter alt for monolith |
| NCronJob | MAYBE | Modern minimal-API style |
| Quartz.NET | LATER | Only when cron-cluster needed |
| Cronos (cron parser) | DONE | Add as core dep |
| Outbox-as-job pattern | NEXT | Wired automatically when messaging is enabled |
| Hangfire Pro | SKIP | Commercial — use OSS path |

### 2.12 Health checks — NOW (P1)

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.Extensions.Diagnostics.HealthChecks` core | LOCKED | First-party |
| Xabaril `AspNetCore.HealthChecks.*` for providers | NOW | Pre-wire SQL/Postgres/Redis/RabbitMQ/ASB/Mongo/Elasticsearch/Network |
| `AspNetCore.HealthChecks.UI` | NEXT | Optional companion |
| K8s liveness / readiness / startup probes | NOW | Three endpoints by convention |
| OTel-compatible health exporter | NEXT | Native metric publish |

### 2.13 Feature flags — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.FeatureManagement` + `.AspNetCore` | NEXT | Default abstraction |
| OpenFeature.NET as alternative seam | MAYBE | Spike — vendor-neutral |
| LaunchDarkly / ConfigCat / GrowthBook / Unleash / Esquio / Flagsmith adapters | LATER | Companion adapters via OpenFeature provider impls |
| Variant assignment + experiment tracking | LATER | A/B-specific |

### 2.14 Multi-tenancy — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| **Finbuckle.MultiTenant** wrapped | NEXT | Default tenancy lib |
| Resolution strategies: subdomain / header / route / JWT claim / default | NEXT | Convention helpers |
| Per-row tenancy via EF Core query filters | NEXT | Default isolation strategy |
| Per-DB tenancy via connection-string-per-tenant | NEXT | Premium tenants |
| Per-schema tenancy | LATER | Edge case |
| Per-tenant cache key prefix | NEXT | Built-in helper |
| Per-tenant config overlay | NEXT | Stack tenant `IConfiguration` provider |
| Per-tenant migrations | LATER | Background job runner |
| ABP / OrchardCore tenants | SKIP | Don't take the framework dep |

### 2.15 Localization / i18n — LATER (P4)

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.Extensions.Localization` (`IStringLocalizer<T>`) | LATER | First-party; defer |
| `.resx` resource files | LATER | Defer until consumer demand |
| ICU MessageFormat for plurals | LATER | Plug-in once needed |
| Humanizer for date/number phrases | NEXT | Lightweight; add early |
| RTL forwarding (UI concern) | SKIP | Out of scope here |
| Pseudo-localization | LATER | QA-only |

### 2.16 Time / clock / culture — NOW (P1)

| Item | Verdict | Note |
|---|---|---|
| `TimeProvider` (.NET 8+) as default abstraction | LOCKED | Public seam |
| `FakeTimeProvider` for tests | DONE | Use `Microsoft.Extensions.TimeProvider.Testing` |
| NodaTime for serious time domain modeling | NEXT | Optional adapter (Instant, ZonedDateTime, IClock) |
| `NodaTime.Serialization.SystemTextJson` | NEXT | Pair with NodaTime |
| TimeZoneConverter (Win↔IANA) | DONE | Add as core dep |
| Cronos (cron parser) | DONE | Add as core dep |
| Humanizer for relative-time phrases | NEXT | |

### 2.17 Error handling / Result types — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| `IExceptionHandler` (.NET 8+) for global | LOCKED | First-party |
| RFC 7807 ProblemDetails everywhere | LOCKED | No custom error envelopes |
| Hellang.Middleware.ProblemDetails (richer mapping) | NOW | Default registration |
| **ErrorOr** as canonical Result type | NEXT | MediatR-friendly, MIT |
| OneOf for ad-hoc DUs | NEXT | Add as core dep |
| FluentResults | SKIP | Pick one — ErrorOr wins |
| LanguageExt.Core | SKIP | Too FP-heavy for default surface; leave to consumers |
| HTTP status mapping convention (400/401/403/404/409/422/429/500) | NOW | Opinionated mapper |

### 2.18 Serialization — DONE (STJ default) + NEXT for binary

| Item | Verdict | Note |
|---|---|---|
| System.Text.Json default | LOCKED | Source-gen everywhere |
| Source-gen `JsonSerializerContext` | LOCKED | AOT-safe |
| `[JsonPolymorphic]` polymorphism | DONE | First-party |
| MessagePack-CSharp for binary RPC payloads | NEXT | Cysharp; source-gen |
| MemoryPack for hot-path internal serialization | LATER | When perf matters |
| Newtonsoft.Json | SKIP from public surface; transitive only |
| Protobuf via `Google.Protobuf` | NEXT | When gRPC adapter ships |
| Avro / FlatBuffers / Cap'n Proto / Bebop | LATER / SKIP | Niche adapters only |
| JsonPath.Net / JsonPatch.Net / JsonPointer.Net / JmesPath.Net (Greg Dennis family) | NEXT | Add as core deps |
| Microsoft.AspNetCore.JsonPatch.SystemTextJson | NEXT | Replace Newtonsoft-based JSON Patch |

### 2.19 Real-time / streaming — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| SignalR + Redis backplane | NEXT | Default real-time stack |
| `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` | NEXT | Default protocol |
| Azure SignalR Service adapter | LATER | Opt-in |
| Server-Sent Events via `Lib.AspNetCore.ServerSentEvents` | NEXT | LLM-streaming sweet spot |
| `System.Net.ServerSentEvents` (.NET 10) | LATER | Switch when GA |
| Native WebSockets (`Microsoft.AspNetCore.WebSockets`) | DONE | First-party |
| MagicOnion | SKIP | Specialist; opt-in companion only |
| MQTTnet | LATER | IoT/messaging niche |

### 2.20 Async / pipelines / dataflow — DONE

`IAsyncEnumerable<T>`, `System.Threading.Channels`, `TPL Dataflow` — all first-party, used internally where appropriate. Rx.NET (`System.Reactive`) — MAYBE, opt-in only. Akka.Streams — SKIP.

### 2.21 Performance / runtime — DONE for sane defaults; NEXT for AOT branch

| Item | Verdict | Note |
|---|---|---|
| `ArrayPool<T>` / `MemoryPool<T>` / `Span<T>` / `Memory<T>` — used internally | DONE | Convention |
| `ValueTask<T>` on hot paths | DONE | |
| Native AOT compatibility | NEXT | Spike each subpath; document AOT-safe matrix |
| ReadyToRun | LATER | Per-deployment choice |
| Object pooling helpers (`Microsoft.Extensions.ObjectPool`) | DONE | First-party |
| Source generators everywhere we can | LOCKED | Mappers, JSON, validators, regex, P/Invoke |
| `BenchmarkDotNet` micro-benchmark set per critical seam | NEXT | CI gate later |

### 2.22 Idempotency & dedupe — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| `Idempotency-Key` header convention + Redis-backed store | NEXT | First-class for write endpoints |
| Inbox dedupe table (per-message-id) | NEXT | Wired with Outbox |
| Outbox row uniqueness | NEXT | Send-once guarantee |

### 2.23 Distributed transactions / Outbox / Inbox — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| **DotNetCore.CAP** as outbox primary (MIT) | NEXT | Multi-broker outbox + retries + inbox; permissive license |
| MassTransit Transactional Outbox | SKIP from core (commercial v9+ announced) | Adapter only as opt-in companion |
| Wolverine TX Outbox | MAYBE | Spike alongside CAP |
| Brighter Outbox | LATER | Alternative path |
| EFCore manual outbox helpers | NEXT | Hand-rolled fallback for non-CAP scenarios |
| 2PC / MSDTC / XA | SKIP | Discouraged — use outbox |

### 2.24 Audit / change tracking — LATER (P3)

| Item | Verdict | Note |
|---|---|---|
| Audit.NET ecosystem | LATER | Add via opt-in companion |
| EntityFrameworkCore.Triggered | LATER | Per-app concern |
| EF SaveChangesInterceptor for created/updated/by | NEXT | Sane default audit columns |
| EF Soft-delete query filter | NEXT | Default opt-in helper |
| SQL Server temporal tables | LATER | Provider-specific |

### 2.25 Testing surface — NEXT (P2)

The lib's own tests deferred (beta-forever rule), but we ship a **`*.testing` companion package** for consumer use:

| Item | Verdict | Note |
|---|---|---|
| WebApplicationFactory<T> wrapper (`WebApiTestHost<TProgram>`) | NEXT | Conventional integration-test scaffold |
| Testcontainers fixtures (Postgres, Redis, RabbitMQ, Mongo, Kafka, Azurite) | NEXT | Pre-built fixtures |
| Verify integrations (snapshot tests) | NEXT | `Verify.AspNetCore`, `.EntityFramework`, `.Http` |
| FluentAssertions v7 (still free) | MAYBE | Or **AwesomeAssertions** (FluentAssertions OSS fork after license shift) |
| AwesomeAssertions | NEXT | OSS fork; safe license |
| Shouldly | MAYBE | Alt to FluentAssertions |
| NSubstitute (preferred) | NEXT | BSD; modern API |
| Moq | SKIP | "SponsorLink" controversy + license drama |
| Bogus | NEXT | Test data |
| Respawn | NEXT | DB reset |
| WireMock.Net | NEXT | HTTP mocking |
| `FakeTimeProvider` | DONE | Already covered |
| FakeLogger / FakeMeter | DONE | First-party |
| BenchmarkDotNet | NEXT | Perf harness |
| NBomber | LATER | Load testing |
| Stryker.NET | LATER | Mutation testing |
| FsCheck / CsCheck | LATER | Property-based |
| Reqnroll (BDD) | SKIP from core | Opt-in only |
| PactNet (contract tests) | LATER | When inter-service comms ship |

### 2.26 AOP / interception / source-gen — DONE (source-gen-first) + LATER for runtime AOP

| Item | Verdict | Note |
|---|---|---|
| MediatR-style pipeline behaviors | NEXT | We ship our own `IPipelineBehavior<,>`-equivalent (MediatR is going commercial) |
| `IEndpointFilter` (Minimal API) | DONE | First-party |
| `IExceptionHandler` (.NET 8+) | DONE | First-party |
| Source generators (Mapperly, Vogen, custom) | LOCKED | Default approach for AOP-style concerns |
| Castle.DynamicProxy runtime AOP | LATER | Only if reflection-based interception is unavoidable |
| Fody / Metalama / PostSharp | SKIP | Build-system invasive |

### 2.27 Modularity — DONE (foundation/domain rule)

| Item | Verdict | Note |
|---|---|---|
| Foundation layer (`tokens`, `primitives`, `utils`, `time`, `errors`) cannot import domains | LOCKED | ESLint-equivalent: enforced via `Roslyn analyzer` |
| Domains can import any sibling domain | LOCKED | Permissive — judgment goes to spec |
| One feature per folder | LOCKED | Mirrors UI lib `numberInput/` pattern |
| Subpath exports per top-level src folder | LOCKED | Same as UI lib |
| OrchardCore tenants / ABP modules | SKIP | Don't take framework dep |

### 2.28 Documentation surface — NEXT (P2)

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.AspNetCore.OpenApi` (.NET 9 first-party) | LOCKED | Default |
| Swashbuckle as fallback | MAYBE | Only if OpenAPI 3.1 gaps require it |
| NSwag for client gen | LATER | Companion tooling |
| Kiota for client gen | LATER | Microsoft path |
| Refitter for OpenAPI → Refit interfaces | LATER | Niche client gen |
| AsyncAPI via Saunter | LATER | Event-driven APIs |
| DocFX for API docs site | LATER | When public docs needed |
| XML doc comments on every public type | LOCKED | `<GenerateDocumentationFile>true</GenerateDocumentationFile>` |

---

## 3. Domain wrapper plan

Mirrors §5 of `ideas.md`. Each domain gets a verdict + the subpath path.

### 3.1 HTTP API surface — NOW (P1)

Subpath: `wow-two-sdk.backend.beta.web` + `.api`.

| Item | Verdict | Note |
|---|---|---|
| Minimal API conventions (preferred default) | LOCKED | |
| Controllers/MVC support | DONE | Inherit |
| FastEndpoints | MAYBE | Spike for opinionated structure; might adopt |
| Carter | MAYBE | Lighter alt; spike |
| Asp.Versioning.{Mvc, Http, Mvc.ApiExplorer} | NOW | Default versioning |
| ProblemDetails everywhere | LOCKED | |
| RFC-7807 error envelope | LOCKED | |
| Sieve / Gridify for filter/sort/page | NEXT | Pick one; lean Gridify |
| OData | LATER | Optional companion |
| GraphQL via HotChocolate | LATER | Companion package — significant surface |
| Webhooks (outbound + inbound, HMAC verify, retry) | NEXT | First-class |
| Pagination conventions (cursor + offset) | NOW | Built-in helpers |
| Idempotency middleware | NEXT | See §2.22 |
| Output formatters (XML, etc.) | LATER | Inherit from MVC if needed |

### 3.2 HTTP client / outbound — NOW (P1)

Subpath: `wow-two-sdk.backend.beta.http`.

| Item | Verdict | Note |
|---|---|---|
| `IHttpClientFactory` typed clients | LOCKED | |
| **Refit** as default declarative client | LOCKED | MIT; AOT-friendly v7+ |
| `Microsoft.Extensions.Http.Resilience` standard handler | LOCKED | Default |
| Standard-Hedging handler | NEXT | Per-route opt-in |
| OTel HttpClient instrumentation | LOCKED | Default |
| Bearer / mTLS / OAuth client-credentials handlers | NEXT | Convention helpers |
| Flurl.Http | MAYBE | Spike — pleasant for ad-hoc |
| RestSharp | SKIP | Refit covers it |
| WireMock.Net for tests | NEXT | In `.testing` package |
| Kiota / NSwag client codegen | LATER | Tooling companion |

### 3.3 Data persistence — relational — NEXT (P2)

Subpath: `wow-two-sdk.backend.beta.data`.

| Item | Verdict | Note |
|---|---|---|
| EF Core 9+ as default ORM | LOCKED | |
| Providers shipped: SqlServer, Npgsql, Pomelo MySQL, Sqlite | NEXT | Multi-provider seam |
| Cosmos provider | LATER | Optional |
| Oracle / Firebird / DB2 | SKIP from core | Niche |
| **Dapper** for hot read paths | NEXT | Add as core dep |
| EFCore.BulkExtensions for bulk ops | NEXT | Default opt-in |
| `ExecuteUpdate/DeleteAsync` (.NET 7+) | DONE | Use freely |
| EFCore.NamingConventions (snake_case for Postgres) | NEXT | Default Postgres convention |
| EntityFrameworkCore.Triggered | LATER | |
| EFCore.Projectables | NEXT | For computed properties |
| Marten as ES + docDB on Postgres | LATER | Optional companion |
| RavenDB / NHibernate / linq2db / RepoDb / ServiceStack.OrmLite | SKIP | Don't fragment |
| Audit columns (CreatedAt/UpdatedAt/By) via SaveChangesInterceptor | NEXT | Sane default |
| Soft-delete query filter helper | NEXT | Default opt-in |
| Multi-tenant query filters | NEXT | See §2.14 |
| Migration runner: EF Migrations + DbUp adapter | NEXT | Both supported |
| FluentMigrator | LATER | Optional |
| Connection resiliency (`EnableRetryOnFailure`) | LOCKED | Default |
| DbContext pooling | LOCKED | Default |
| AOT EF Core (.NET 9 preview) | LATER | Promote when stable |

### 3.4 Data persistence — NoSQL / specialty — LATER (P3)

Subpath: `wow-two-sdk.backend.beta.data.nosql`.

MongoDB.Driver, RavenDB.Client, Cassandra, Cosmos, Marten, EventStore.Client (Kurrent), InfluxDB, ClickHouse, Pgvector, Redis Stack — all LATER. Add adapters as consumer demand surfaces.

### 3.5 Messaging / integration — NEXT (P2)

Subpath: `wow-two-sdk.backend.beta.messaging`.

| Item | Verdict | Note |
|---|---|---|
| In-process mediator (`IRequestHandler<,>` / `INotificationHandler<>`) | NEXT | Ship our own — MediatR-API-compatible facade since MediatR is going commercial |
| `Mediator` (martinothamar) source-gen alt | MAYBE | Spike vs hand-rolled |
| MediatR | SKIP | Commercial 12.0+ |
| **DotNetCore.CAP** for outbox + multi-broker | NEXT | MIT; default distributed messaging |
| **MassTransit** | SKIP from core | Commercial v9+ announced; opt-in adapter only |
| Wolverine | MAYBE | OSS modern alt; spike vs CAP |
| NServiceBus / Brighter / Rebus | SKIP from core | Adapters only |
| RabbitMQ.Client / Confluent.Kafka / Azure.Messaging.ServiceBus / NATS.Client / MQTTnet | NEXT | Direct broker clients shipped as adapters |
| AWS SQS/SNS / GCP Pub/Sub | LATER | Companion |
| Sagas / state machines (Stateless lib) | NEXT | Stateless is MIT, well-maintained |
| MassTransit Sagas / Wolverine state machines | SKIP / MAYBE | License + scope |

### 3.6 Real-time / push — see §2.19

### 3.7 Storage / files / media — LATER (P3) *(except `Media.Captions`, shipped NOW)*

Subpath: `wow-two-sdk.backend.beta.storage` + `.media`.

| Item | Verdict | Note |
|---|---|---|
| Azure.Storage.Blobs | LATER | Adapter |
| AWSSDK.S3 | LATER | Adapter |
| MinIO.SDK | LATER | Adapter |
| FluentStorage abstraction over multi-cloud | NEXT | Single API across S3/Azure/GCS/MinIO |
| ImageSharp (+`.Web`) | NEXT | Default image processing — note >1M$ commercial threshold; document it |
| SkiaSharp / Magick.NET | LATER | Format-specific |
| **QuestPDF** for PDF generation | NEXT | MIT (commercial threshold matches ImageSharp) |
| iText7 / Aspose / Spire | SKIP | License |
| ClosedXML for Excel | NEXT | MIT; default xlsx |
| EPPlus | SKIP | Polyform commercial |
| OpenXML SDK | DONE | First-party |
| CsvHelper | NEXT | Default CSV |
| Sep / Sylvan.Data.Csv | LATER | Hot-path perf alts |
| Markdig (Markdown) | NEXT | Default markdown |
| HtmlAgilityPack / AngleSharp | LATER | HTML parsing adapters |
| MimeKit / MailKit | NEXT | Email |
| Xabe.FFmpeg / FFMpegCore | LATER | Optional |
| Tesseract / Aspose.OCR | LATER | OCR companion |
| **`Media.Captions`** — WebVTT parse (own logic, pure, zero-dep) | **NOW** | Shipped `src/Media/Captions/` — extracted from the TranscriptForge POC (1st proven consumer). VTT-only; SRT/TTML/JSON3 + a multi-format `ICaptionParser` facade when a 2nd format is needed. Own logic, not a lib wrapper — the SDK holds domain logic too |
| `Media.Audio` — yt-dlp / ffmpeg extraction | LATER | Thin process wrapper; extract alongside STT when a 2nd consumer/impl appears (TranscriptForge-inline now) |
| `Media.Transcripts` — caption→STT orchestrator | LATER | Composite crown-jewel; gated on Captions + Audio + STT all extracting (TranscriptForge-inline now) |

### 3.8 AI / ML / LLM / vector — NEXT (P2)

Subpath: `wow-two-sdk.backend.beta.ai`.

| Item | Verdict | Note |
|---|---|---|
| `Microsoft.Extensions.AI` + `.Abstractions` | LOCKED | Default abstraction (.NET 9) |
| `Microsoft.SemanticKernel` | NEXT | Plugin orchestration |
| `Microsoft.KernelMemory` | LATER | Optional RAG service |
| Provider adapters: OpenAI, Azure.AI.OpenAI, Anthropic.SDK, AWS Bedrock, Google Vertex/Gemini (via Mscc.GenerativeAI), OllamaSharp, LLamaSharp | NEXT | Pluggable per `IChatClient` |
| Vector store adapters: Pinecone.NET, Qdrant.Client, Pgvector for Npgsql, Redis Vector via NRedisStack, Milvus, Chroma, Microsoft.SemanticKernel.Connectors.* | NEXT | Per `IVectorStore<TKey, TRecord>` (Microsoft.Extensions.VectorData) |
| ML.NET | LATER | Classical ML companion |
| ONNX Runtime | LATER | Local inference companion |
| LangChain.NET | SKIP | Less mature than `Microsoft.Extensions.AI` |
| Whisper.net (local STT) | LATER | Companion |
| `Ai.SpeechToText` — provider abstraction (`ISpeechToTextProvider`) | LATER | Interface + Groq-Whisper impl live in the TranscriptForge POC; promote when a 2nd provider (OpenAI / Azure / Whisper.net) appears — extract-the-interface-on-2nd-impl rule |
| Tokenizers (`Microsoft.ML.Tokenizers`) | NEXT | Cost tracking, prompt fitting |
| Cost tracking + OTel meter for token usage | NEXT | First-class |
| Embedding cache + semantic-similar response cache | NEXT | Built-in |
| Microsoft.AI.Evaluation (.NET 9) | LATER | Eval framework |
| MCP (Model Context Protocol) integration | NEXT | Anthropic standard; emerging |

### 3.9 Authentication / Authorization deep — see §2.8

### 3.10 Workflow / state machines — LATER (P3)

| Item | Verdict | Note |
|---|---|---|
| **Stateless** for in-process state machines | NEXT | MIT, well-maintained |
| Microsoft.DurableTask | LATER | Optional companion |
| Elsa Workflows / WorkflowCore / Optimajet | SKIP | Heavy framework deps |
| Camunda / Temporal SDKs | LATER | When external orchestrator is needed |

### 3.11 Documents / reports / PDFs / Excel — see §3.7

### 3.12 Geo / IP / maps — LATER (P3)

| Item | Verdict | Note |
|---|---|---|
| NetTopologySuite | NEXT | Default geometry types |
| Geocoding.Net facade | LATER | |
| MaxMind.GeoIP2 | NEXT | Default IP→geo |
| H3.Net / Geohash.Net / OpenLocationCode.Net | LATER | Spatial indexing |
| ProjNet | LATER | SRID conversions |
| BAMCIS.GeoJSON / GeoJSON.Net | NEXT | (de)serialization |

### 3.13 Email / SMS / push — NEXT (P2)

Abstractions: `IEmailSender`, `ISmsSender`, `IPushSender`. Default impls: **MailKit** + **MimeKit** (SMTP), **Twilio** (SMS), **FirebaseAdmin** (FCM), **dotAPNS** (Apple). Optional adapters: SendGrid, Mailgun, Postmark, AWS SES, Azure Communication Email/SMS, Vonage, Plivo, MessageBird, OneSignal. **FluentEmail** as the templating layer (Razor-friendly).

### 3.14 Payments / billing — LATER (P3)

| Item | Verdict | Note |
|---|---|---|
| `IBillingProvider` abstraction | LATER | First-class seam |
| **Stripe.net** as primary adapter | LATER | Default once shipped |
| Paddle / Braintree / Adyen / ChargeBee / Recurly / Square | LATER | Per-vendor adapters |
| Webhook signature verification + replay protection + idempotency | NEXT | Generic infra (used by all payment + non-payment webhooks) |

### 3.15 Search — LATER (P3)

| Item | Verdict | Note |
|---|---|---|
| Elastic.Clients.Elasticsearch / OpenSearch.Client | LATER | Pick one path; OpenSearch likely default |
| Postgres tsvector via Npgsql | NEXT | Default for moderate scale |
| Pgvector hybrid search | NEXT | Pair with §3.8 |
| Lucene.Net (embedded) | LATER | Niche |
| Algolia / Meilisearch / Typesense | LATER | Hosted-search adapters |
| Azure.Search.Documents | LATER | Adapter |

### 3.16 Configuration / secrets deep — see §2.2

### 3.17 Realtime metrics / SLO — see §2.7

### 3.18 Multi-tenancy deep — see §2.14

### 3.19 i18n deep — see §2.15

### 3.20 Static analysis / dev experience — NOW (P1)

Ship `.editorconfig` + analyzer pack:
- `Microsoft.CodeAnalysis.NetAnalyzers` (default, in SDK)
- `Microsoft.VisualStudio.Threading.Analyzers` (NOW)
- `StyleCop.Analyzers` (NEXT)
- `SonarAnalyzer.CSharp` (LATER, LGPL caveat — confirm OK)
- `Roslynator.Analyzers` (LATER)
- **Meziantou.Analyzer** (NOW, MIT — most-recommended modern pack)
- `IDisposableAnalyzers` (NEXT)
- `AsyncFixer` (NEXT)
- `JetBrains.Annotations` (NEXT — for nullability/contract attrs)

---

## 4. Delegate / extension API canonization — NEXT

These shapes will be canonized and named consistently across the package. Lifted from §6 of `ideas.md`.

| Shape | Verdict | Canonical name pattern |
|---|---|---|
| `Func<T, ValueTask<TResult>>` async transform | NEXT | `AsyncTransform<TIn, TOut>` |
| `Action<TBuilder>` / `Action<TOptions>` configurators | NEXT | `Configure*` |
| `IPipelineBehavior<TIn, TOut>` mediator behavior | NEXT | Match MediatR-API |
| `IEndpointFilter` for HTTP cross-cuts | DONE | First-party |
| `IExceptionHandler` | DONE | First-party |
| `IAuthorizationHandler` | DONE | First-party |
| `IDbCommandInterceptor` / `ISaveChangesInterceptor` | DONE | EF Core |
| `IHealthCheck` | DONE | First-party |
| Predicate `Func<T, bool>` / `Func<T, Task<bool>>` | DONE | Convention |
| Comparer `IEqualityComparer<T>` / `IComparer<T>` | DONE | Convention |
| `IObservable<T>` / `IObserver<T>` | LATER | Rx opt-in |
| `Action<ILoggerBuilder>` / `Action<IConfigurationBuilder>` | DONE | First-party |
| `Func<IServiceProvider, T>` factory | DONE | First-party |
| `IValidator<T>` (FluentValidation) | LOCKED | First-class |
| `IMessageHandler<T>` / `IConsumer<T>` | NEXT | Match canonical name once messaging ships |
| Source-gen attribute hooks | NEXT | `[Mapper]`, `[ValueObject]`, `[LoggerMessage]` (existing) |

---

## 5. Configuration leak point inventory verdict — NEXT

Mirrors §7 of `ideas.md`. For each leak point, define a typed `record TFooOptions(...)` with source-gen `[OptionsValidator]` validation. Group: hosting, auth, DB, cache, messaging, storage, email, SMS/push, search, vector, LLM provider, telemetry, logging sinks, health checks, rate limiting, jobs, feature flags, multi-tenancy, localization, CORS, compression, anti-forgery, DataProtection, HSTS, static files, OpenAPI, gRPC, GraphQL, SignalR, resilience, webhook delivery.

Convention: each module exports `services.AddWowFoo(o => { ... })`. Each option type lives in `wow-two-sdk.backend.beta.{module}/Options/`.

---

## 6. Phase mapping (revised P0–P6, dependency-aware + value-first)

Decoupled from UI lib's P1–P6. Reasoning:

- **Identity moved out of "data + messaging"** — most APIs need auth from day 1 without taking on EF Core.
- **Testing extracted to P0 (parallel track)** — every wrapper must ship with runnable examples; testing scaffold can't be a Phase-4 afterthought.
- **Mediator + ProblemDetails promoted** — these are seams everything else hangs on, not P2 add-ons.
- **Real-time / search / workflow / media demoted** — domain-specific; most apps need 0–2.

| Phase | Bundle | Scope (NOW + NEXT items) | Stop here if… |
|---|---|---|---|
| **P0 — testing scaffold (parallel track)** | `testing` companion package | `WebApiTestHost<T>` + Testcontainers fixtures (Postgres, Redis, RabbitMQ, Mongo, Kafka, Azurite) + Verify + AwesomeAssertions + Bogus + WireMock.Net | n/a — never stops; grows with every wrapper |
| **P1 — boot floor** | foundation + `observability` + `web` (basics) | Hosting / DI conventions / Options + source-gen validation / Serilog→`ILogger<T>` seam / OTel SDK + auto-instrumentation / ProblemDetails (built-in + Hellang) / `Microsoft.AspNetCore.OpenApi` / Health checks (Xabaril core providers) / Rate limiting / Output cache (HybridCache memory mode) / Secure-headers middleware / `TimeProvider` + Cronos / Analyzer pack + `.editorconfig` | "Hello World API with logs/traces/health/openapi/rate-limit/problemdetails" — broadest applicability, lowest risk |
| **P2 — request pipeline + auth** | `mediator` + `validation` + `errors` + `identity` | In-proc mediator (our facade, MediatR-API-compat) / FluentValidation pipeline behavior / `IExceptionHandler` global / **Identity API endpoints + JWT bearer + OIDC** / Vogen + Ardalis.GuardClauses / ErrorOr Result type / OneOf / Idempotency middleware | Stateless API (microservice doing HTTP + JWT + downstream calls) — no DB needed |
| **P3 — persistence + outbound** | `data` + `caching` + `http` | EF Core (SqlServer/Npgsql/Pomelo/Sqlite) + audit interceptor + soft-delete filter / Dapper / EFCore.BulkExtensions / EFCore.NamingConventions / EFCore.Projectables / HybridCache + Redis L2 / Refit + `Microsoft.Extensions.Http.Resilience` / DbUp + EF Migrations runners | Single-instance API with persistence — no horizontal scale |
| **P4 — distributed essentials** | `messaging` + `jobs` + `comms` | DotNetCore.CAP outbox + inbox / Hangfire (Postgres/Redis storage) / Comms abstractions (`IEmailSender` MailKit, `ISmsSender` Twilio, `IPushSender` FCM/dotAPNS) / Webhook delivery infra (HMAC + retry + DLQ) | Multi-instance reliable distributed services |
| **P5 — multi-tenant + AI + flags** | `tenancy` + `ai` + `feature-flags` | Finbuckle multi-tenancy (per-row default, per-DB option) / `Microsoft.Extensions.AI` + Semantic Kernel + provider adapters / vector store adapters (Pgvector, Redis, Qdrant, Pinecone, Milvus, Chroma) / `Microsoft.FeatureManagement` + OpenFeature seam | SaaS-shaped products with AI features |
| **P6 — heavy domain extensions** | `realtime` + `storage` + `search` + `workflow` + extras | SignalR + Redis backplane + SSE / FluentStorage + ImageSharp + QuestPDF + ClosedXML + CsvHelper + Markdig / Postgres tsvector + OpenSearch adapter / Stateless workflow / Aspire integrations / OpenIddict OIDC server adapter / GraphQL (HotChocolate) / OData / AsyncAPI / Kiota client gen / Native AOT certification / Payment adapters (Stripe.net) / Geo (NetTopologySuite + MaxMind) / Email templating (FluentEmail) / OCR (Tesseract) / Documents (DocFX site) | Domain-complete platform |

**Sequencing principle**: each phase is shippable on its own. Drop out at any phase if your app doesn't need the next layer. P0 grows continuously alongside P1–P6.

---

## 7. Companion package roadmap (out of core)

Things deliberately **not** in the meta-package. Either license-incompatible, scope-heavy, or niche.

| Companion | Reason |
|---|---|
| `wow-two-sdk.backend.beta.testing` | Test-only; keep core lean |
| `wow-two-sdk.backend.beta.aspire-azure` | Azure-only adapters |
| `wow-two-sdk.backend.beta.aspire-aws` | AWS-only adapters |
| `wow-two-sdk.backend.beta.identity-duende` | Commercial license |
| `wow-two-sdk.backend.beta.identity-openiddict` | OSS alt to Duende |
| `wow-two-sdk.backend.beta.messaging-masstransit` | Commercial (v9+) |
| `wow-two-sdk.backend.beta.messaging-wolverine` | Modern OSS alt |
| `wow-two-sdk.backend.beta.graphql-hotchocolate` | Heavy surface |
| `wow-two-sdk.backend.beta.odata` | Niche |
| `wow-two-sdk.backend.beta.event-sourcing-marten` | Postgres-specific ES |
| `wow-two-sdk.backend.beta.event-sourcing-kurrent` | EventStoreDB |
| `wow-two-sdk.backend.beta.workflow-elsa` / `.workflow-temporal` | Heavy workflow engines |
| `wow-two-sdk.backend.beta.media-ffmpeg` | Native deps |
| `wow-two-sdk.backend.beta.media-ocr` | Tesseract / OCR |
| `wow-two-sdk.backend.beta.search-elastic` / `.search-opensearch` | Cluster-grade search |
| `wow-two-sdk.backend.beta.payments-stripe` | Commerce-domain |
| `wow-two-sdk.backend.beta.docs` | DocFX site generator |
| `wow-two-sdk.backend.beta.cli` | Spectre.Console-based CLI scaffolding |

---

## 8. Out-of-scope — explicit list

Things we deliberately won't build, with rationale.

| Item | Rationale |
|---|---|
| Browser / mobile clients | UI lib's job |
| F# idiomatic surface | C#-first; F# can consume |
| Game engine integration (Unity/Godot) | Out of scope for backend |
| Service Fabric integration | Legacy |
| WCF / SOAP servers | CoreWCF community handles it; SKIP |
| Blazor UI components | UI lib |
| Office Interop (Microsoft.Office.Interop.\*) | Win-only, awful API; use OpenXML instead |
| BinaryFormatter / NRBF write | Insecure; only NRBF read for legacy |
| MSMQ transport | Legacy |
| WebForms / ASP.NET 4.x | Legacy |
| Razor.Web Pages legacy | Use Razor Pages or Blazor |
| Custom DI container | MS DI is the contract |
| Custom JSON serializer | STJ is the contract |
| Custom logging facade | `ILogger<T>` is the contract |
| Twitter/X authentication | Sunset API v1.1; use the X v2 OIDC if needed |
| IdentityServer4 integration | EOL |
| Personalizer / MetricsAdvisor / AnomalyDetector adapters | Azure retiring 2026 |

---

## 9. Decisions still pending — resolve before P3

| # | Decision | Notes |
|---|---|---|
| 9.1 | Mediator: hand-rolled facade vs `Mediator` (martinothamar) source-gen | Spike both; perf + license matter |
| 9.2 | Outbox: DotNetCore.CAP vs Wolverine | Both MIT; CAP simpler scope, Wolverine more capable |
| 9.3 | FluentAssertions v7 vs AwesomeAssertions vs Shouldly | License pressure on FA v8+; pick OSS path |
| 9.4 | Default cache: HybridCache vs FusionCache | FusionCache richer; HybridCache first-party |
| 9.5 | OutputCache adapter: `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis` vs custom HybridCache adapter | Redundancy if HybridCache covers it |
| 9.6 | OpenAPI: `Microsoft.AspNetCore.OpenApi` only vs Swashbuckle fallback | OpenAPI 3.1 gaps in MS impl? Verify |
| 9.7 | Multi-tenancy default: per-row vs per-DB | Pick one as default; offer both |
| 9.8 | Identity API endpoints (.NET 8+ minimal) vs full ASP.NET Core Identity scaffolding | Likely both, with the minimal API endpoints as the bundled default |
| 9.9 | Real-time default: SignalR alone vs SignalR + SSE side-by-side | Both — SignalR for bidi, SSE for LLM streams |
| 9.10 | gRPC: ship as core or as companion | Bundle adds ~medium cost; might be companion |
| 9.11 | NodaTime: core dep vs companion | If included, all internal time types use it; spec-impact |
| 9.12 | License audit on every transitive dep | One-pass check before P1 ships; document |
| 9.13 | AOT compatibility matrix per subpath | Spike each; document support level |
| 9.14 | Source-gen vs Roslyn analyzer enforcement of foundation/domain rule | UI lib uses ESLint; we need a Roslyn analyzer |
| 9.15 | Whether to include Aspire integrations in core or only as companion | Aspire is the orchestration story; likely companion |

---

## 10. Sequencing summary (TL;DR)

**P0 (parallel track, ongoing)** — `testing` companion: `WebApiTestHost<T>` + Testcontainers fixtures + Verify + AwesomeAssertions + Bogus + WireMock.Net. Ships *with* P1 so every wrapper has runnable examples on day 1.

**P1 (boot floor)** — broadest-applicability defaults: Hosting / DI / Options + validators / Serilog→ILogger seam / OTel auto-instrumentation / ProblemDetails / `Microsoft.AspNetCore.OpenApi` / Health checks / Rate limiting / Output cache / Secure headers / `TimeProvider` / Analyzer pack.

**P2 (request pipeline + auth)** — the seam everything else hangs on: in-proc mediator / FluentValidation pipeline / `IExceptionHandler` / Identity API endpoints + JWT + OIDC / Vogen + Guard clauses / ErrorOr + OneOf / Idempotency.

**P3 (persistence + outbound)** — EF Core + audit/soft-delete + Dapper + bulk + HybridCache + Redis + Refit + resilience.

**P4 (distributed essentials)** — DotNetCore.CAP outbox + Hangfire jobs + comms abstractions + webhook infra.

**P5 (SaaS-shaped)** — Finbuckle multi-tenancy + `Microsoft.Extensions.AI` + Semantic Kernel + vector store adapters + feature flags.

**P6 (heavy domain extensions)** — SignalR + storage + search + workflow + Aspire integrations + OpenIddict + GraphQL + OData + AOT cert + payments + geo + OCR + docs site.

Beta-forever rule applies throughout — fix-forward, no version gates, push to main, breaking changes go in normally.
