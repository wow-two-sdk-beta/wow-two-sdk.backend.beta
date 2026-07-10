# Package registry

*Last updated: 2026-06-23*

> **Mono-lib note:** since the mono-lib migration these "packages" are subpath areas of the single
> `WoW2.Sdk.Backend.Beta` NuGet (plus the separate `.Testing` and `.Testing.Data` libs). Rows still track per-area status.

> Lookup table of every NuGet package this repo produces.
> Status: **stub** = csproj exists, no impl · **scaffold** = registration + minimal API · **shipped** = real wrapper, tested · **planned** = not yet started.

## Meta — composition root

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta` (root, `src/Meta/`) | `AddApiDefaults()` / `UseApiDefaults()` — one-import P1 boot floor (Serilog + OTel + health + proxy hosting + OpenAPI + problem details + rate limit + output cache + compression + optional CORS/validators). Auth/mediator/data stay explicit. | shipped |

## P0 — Testing scaffold (parallel track)

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Testing` | Core test host (`WebApiTestHost<TProgram>`), `WebApiTestBase<TProgram>` xUnit base, async fixture interfaces | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Assertions` | Bundles AwesomeAssertions + Shouldly | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Bogus` | `BogusFakerFactory` deterministic-seed factory | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Verify` | `VerifyDefaults.Initialize()` + Verify.{Xunit, AspNetCore, Http} | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.WireMock` | `WireMockFixture` (WireMock.Net async fixture) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers` | `ContainerFixtureBase<TContainer>` for engine packages | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres` | `PostgresFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Redis` | `RedisFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.RabbitMq` | `RabbitMqFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Kafka` | `KafkaFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.MongoDb` | `MongoDbFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Azurite` | `AzuriteFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Data` | Data-tier helpers (companion → core + EF): `MigratorHarness` (PG + SQLite), `MigratorPostgresFixture` (drop-schema reset), `MigratorTestBase`, `MigrationsWorkspace`, `MigrationHistoryRow`, `DisableBespokeMigrator()`, `RemoveAllForDbContext`/`RepointDbContext` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Elasticsearch` | Elasticsearch Testcontainers fixture | planned |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Localstack` | LocalStack (AWS emulator) fixture | planned |

## P1 — Boot floor

### Foundation

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Time` | `AddTimeProviders`, `TimeZoneHelpers`, `CronExpressionParser`; NodaTime + Cronos + TimeZoneConverter | shipped |
| `WoW.Two.Sdk.Backend.Beta.Errors` | `DomainError` record + category-to-HTTP-status mapping | shipped |
| `WoW.Two.Sdk.Backend.Beta.Results` | Re-exports ErrorOr + OneOf + Ardalis.Result | shipped |
| `WoW.Two.Sdk.Backend.Beta.Validation` | `AddFluentValidatorsFromAssemblies` (FluentValidation assembly scan) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Serialization` | `JsonOptionsPresets` (camelCase + NodaTime + lenient input) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Guards` | `IdentifierGuardExtensions` (`NotSlug`, `NotUlid`) on top of Ardalis.GuardClauses | shipped |
| `WoW.Two.Sdk.Backend.Beta.ValueObjects` | Re-exports Vogen + StronglyTypedId source generators | shipped |
| `WoW.Two.Sdk.Backend.Beta.Naming` | **Casing authority** — `CaseStyle` enum (10 styles), `WordTokenizer` (acronym/digit-aware split), `CaseConverter` + string extensions, reversible `EnumNameConverter<TEnum>`. Zero deps. Source for column / enum-label / SQL-identifier casing. | shipped |

### Observability

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Observability` | Meta — wires logging + tracing + metrics + health | planned |
| `WoW.Two.Sdk.Backend.Beta.Observability.Logging` | `UseSerilogConventional` — Serilog → `ILogger<T>` w/ Console + rolling File + enrichers | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Tracing` | `AddOpenTelemetryTracing` — OTel tracer + AspNetCore/HttpClient/Grpc/SqlClient/EFCore/Redis instrumentation | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Metrics` | `AddOpenTelemetryMetrics` — OTel meter + AspNetCore/HttpClient/Runtime/Process | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks` | `AddHealthChecksBuilder` — `IHealthChecksBuilder` + Xabaril provider deps (SqlServer/Postgres/MySql/Redis/RabbitMQ/Mongo/Kafka/Elastic/Network/Uris/AzureSB/AzureStorage/AwsS3/AwsSqs) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Otlp` | `AddOtlpExporters` — OTLP gRPC/HTTP for traces + metrics | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Prometheus` | `AddPrometheusMetricsExporter` — scrape endpoint | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.AzureMonitor` | `AddAzureMonitorExporter` — Azure Monitor distro | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Datadog` | `Datadog.Trace` re-export (use OTLP route by default) | shipped |

### Web

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Web` | Meta — wires hosting + openapi + problem details + secure-headers + cors + ratelimit + outputcache | planned |
| `WoW.Two.Sdk.Backend.Beta.Web.Hosting` | `AddProxyAwareHosting` / `UseProxyAwareHosting` — forwarded headers + host filtering (`AllowedHosts`) + bounded request limits + request decompression | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.RequestLimits` | `AddRequestLimits` — Kestrel body/header/request-line/headers-timeout caps (bounds the decompression input) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.OpenApi` | `AddOpenApiDefaults` / `MapOpenApiEndpoint` — `Microsoft.AspNetCore.OpenApi` (.NET 9) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.OpenApi.Swashbuckle` | Swashbuckle fallback adapter | planned |
| `WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails` | `AddTraceAwareProblemDetails` — RFC 7807 + traceId/requestId enrichment | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.RateLimit` | `AddPerIpSlidingWindowRateLimit` — sliding window per-IP, 100 req/min default | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.OutputCache` | `AddDefaultOutputCache` — built-in middleware with default 60s policy | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders` | `UseOwaspSecureHeaders` — OWASP-flavored preset (HSTS, CSP-friendly, COOP/COEP/CORP) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.Cors` | `AddDefaultCorsPolicy` — single-line CORS policy registration | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.Compression` | `AddBrotliGzipCompression` — Brotli + Gzip with `Fastest` level | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.Versioning` | `AddDefaultApiVersioning` — URL/header/query versioning with `1.0` default | shipped |

## P2 — Request pipeline + auth

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Mediator` | `IMediator` + `IRequest<T>` + `IPipelineBehavior<,>` — MediatR-API-compatible facade (no MediatR dep, MIT) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Validation` | `AddMediatorValidationBehavior()` — FluentValidation pipeline behavior | shipped |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Logging` | `AddMediatorLoggingBehavior()` — source-gen `[LoggerMessage]` request/response logging | shipped |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Authorization` | `AddMediatorAuthorizationBehavior()` + `IRequireAuthorization` marker | shipped |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency` | `AddMediatorIdempotencyBehavior()` + `IIdempotent` + pluggable `IIdempotencyStore` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity` | Meta — JWT + cookies + Identity API endpoints | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.Core` | `AddUserAccounts<TUser>()` — own sliced-lego user model: 7 entities + `ApplyIdentitySchema` + `IUserStore`/`EfUserStore` + `UserAccountManager` facade (step 1 of the identity rebuild) | shipped (mono-lib) |
| `WoW.Two.Sdk.Backend.Beta.Identity.Jwt` | `AddJwtBearerAuthentication()` — JWT bearer (symmetric or JWKS) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Cookies` | `AddCookieAuthentication()` — secure cookie defaults | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Oidc` | `AddOpenIdConnectAuthentication()` — Authorization Code + PKCE | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google` | `AddGoogleAuthentication(clientId, clientSecret)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Microsoft` | `AddMicrosoftAuthentication(clientId, clientSecret)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitHub` | `AddGitHubAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Apple` | `AddAppleAuthentication(clientId, teamId, keyId, keyPath)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Facebook` | `AddFacebookAuthentication(appId, appSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.LinkedIn` | `AddLinkedInAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Discord` | `AddDiscordAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Slack` | `AddSlackAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitLab` | `AddGitLabAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Amazon` | `AddAmazonAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Twitch` | `AddTwitchAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Spotify` | `AddSpotifyAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Yandex` | `AddYandexAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Reddit` | `AddRedditAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Notion` | `AddNotionAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Vkontakte` | `AddVkontakteAuthentication(clientId, clientSecret, scopes)` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.IdentityApi` | `AddIdentityApiEndpoints<TContext>()` — bearer-token Identity endpoints with hardened defaults | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp` | `TotpService` — secret gen + otpauth URI + verify with ±1 step tolerance | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Mfa.WebAuthn` | `AddFido2WebAuthn(domain, name, origins)` — Fido2.AspNet | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2` | `UseArgon2PasswordHasher<TUser>()` — OWASP 2024 baseline | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Policies` | `AddRolePolicy()` — `IRolePolicy` + `DictionaryRolePolicy` default (scope → allowed roles) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance` | `AddJwtTokenIssuance()` — `ITokenIssuer` HMAC issuance (HS256/384/512), per-call lifetime/audience overrides; pure lib, no ASP.NET dependency | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Otp` | `AddOtpService()` — `IOtpService` create/verify keyed by (subject, scope); `IOtpStore` (memory default) + `IOtpCodeGenerator` (crypto-random numeric) + `IOtpDeliveryHandler` seam; fixed-time compare, rate limit, max attempts | shipped |
| `WoW.Two.Sdk.Backend.Beta.Identity.Otp.Telegram` | `AddTelegramOtpDelivery()` — chat-id delivery via consumer-registered `ITelegramBotClient` (Telegram.Bot, MIT) | shipped |

## P3 — Persistence + outbound

### Data

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Data` | Meta — wires Abstractions + EF Core + Audit + SoftDelete + NamingConventions + Json + Ef Migrations runner | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.Abstractions` | Entity contracts — `IEntity`, `IKeyedEntity<TId>`, `IHasTableName` (static-abstract `TableName`), `IAuditable`/`ICreationAuditable`/`IModificationAuditable` (+ `…By<TUserId>` actor variants), `ISoftDeletable`/`ISoftDeletableBy<TUserId>`, `IHasTenant<TTenantId>`, `IRowVersioned`, `IHasXmin`, `IVersioned`; repository contracts `IReadRepository<TEntity,TId>` + `IRepository<TEntity,TId>` (Create/Update/Delete/Get verbs). Zero deps. | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore` | `AppDbContextBase` + `AddEntityFrameworkCore<TContext>` (pooling + config scanner) | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SqlServer` | `UseSqlServerConventional` — retry-on-failure (6×), 30s command timeout | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres` | `UseNpgsqlConventional` — retry + NpgsqlDataSource overload for enum mapping; `AddNpgsqlDataSource`; `MapEnums(style, nsFilter, …, assemblies)` (reflection bulk enum registration via casing engine, kills double-registry) + `CaseStyleNameTranslator`; `ApplyNpgsqlConventions` (xmin token) | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.MySql` | `UseMySqlConventional` — Pomelo + AutoDetect server version | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite` | `UseSqliteConventional` — 30s command timeout | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Cosmos` | `UseCosmosConventional` — connection-string + endpoint+key overloads | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit` | `AuditInterceptor` (SaveChangesInterceptor) + `IAuditCurrentUserAccessor` for `CreatedBy`/`UpdatedBy` | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete` | `SoftDeleteInterceptor` (DELETE→UPDATE) + `ApplySoftDeleteFilter` ModelBuilder ext | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.NamingConventions` | `UseSnakeCase/LowerCase/CamelCase/UpperSnakeCaseNamingConvention` | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json` | `JsonValueConverter<T>` + `JsonValueComparer<T>` + `HasJsonConversion()` PropertyBuilder ext | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore` (Naming) | `EnumCaseConverter<TEnum>` (reversible string enum via `Naming`) + `HasEnumStringConversion()` PropertyBuilder ext | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore` (Repositories) | `EfRepository<TEntity,TId>` (thin CRUD) + `AddEfRepositories<TContext>` / `AddEfRepository<TRepo,TEntity,TId>` DI helpers | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Bulk` | EFCore.BulkExtensions re-export (BulkInsert/Update/Delete) | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Triggered` | `UseTriggersConventional` + `AddTriggersFromAssemblies` | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Projectables` | `UseProjectablesConventional` — `[Projectable]` computed properties | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.Dapper` | `AddDapperConventions` (snake_case + DateOnly + ListTypeHandler), `IDbConnectionFactory`, `SqlNaming` (`Col`/`Par`/`Table`, string + expression overloads), `EnumTypeHandler<TEnum>` (reversible string enum, `AddEnumTypeHandler<T>`); `DapperRepository<TEntity,TId>` (thin CRUD, SQL from `IHasTableName`+`SqlNaming`) + `AddDapperRepository` DI helpers | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef` | `AddEfMigrationsRunner<TContext>` — hosted service with connect-retry | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp` | `AddDbUpRunner` — hosted service + provider factories (Postgres/SqlServer/MySql/Sqlite) | scaffold |

### Caching

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Caching` | Meta — HybridCache defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Caching.Hybrid` | Microsoft.Extensions.Caching.Hybrid wiring | planned |
| `WoW.Two.Sdk.Backend.Beta.Caching.Memory` | In-process IMemoryCache wiring | planned |
| `WoW.Two.Sdk.Backend.Beta.Caching.Redis` | StackExchange.Redis as L2 backend | planned |
| `WoW.Two.Sdk.Backend.Beta.Caching.SqlServer` | SQL Server as L2 backend | planned |
| `WoW.Two.Sdk.Backend.Beta.Caching.Cosmos` | Cosmos as L2 backend | planned |
| `WoW.Two.Sdk.Backend.Beta.Caching.FusionCache` | FusionCache as alt | planned |

### Http / outbound

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Http` | Meta — Refit + resilience defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Http.Refit` | `AddRefitApiClient<TApi>(baseAddr, …)` — Refit client + SDK JSON + resilience baked in; `CreateDefaultRefitSettings()` | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Http.Resilience` | `AddSdkResilience()` (`IHttpClientBuilder` ext) + tunable `HttpResilienceOptions` over `Microsoft.Extensions.Http.Resilience` (Polly v8) | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Http.Core` | `AddResilientClient<TClient>` / `AddResilientClient(name, …)` — plain typed/named HttpClient + SDK resilience | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Http.Hedging` | `AddSdkHedging()` — standard-hedging preset (parallel attempt racing) + tunable `HttpHedgingOptions`; for idempotent latency-sensitive calls | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Http.HeaderPropagation` | `AddConventionalHeaderPropagation(…)` + `AddPropagatedHeaders()` — X-Correlation-Id / X-Request-Id defaults over `Microsoft.AspNetCore.HeaderPropagation` | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Http.Auth.OAuth2ClientCredentials` | `AddOAuth2ClientCredentials()` — client_credentials bearer handler; per-client-name token cache, single-flight fetch, refresh skew | scaffold |
| `WoW.Two.Sdk.Backend.Beta.Http.Auth.Mtls` | `AddMutualTls()` — client-certificate `SocketsHttpHandler` (loaded cert or PKCS#12 path) | scaffold |

## P4 — Distributed essentials

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Messaging` | Meta — DotNetCore.CAP outbox defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Cap` | DotNetCore.CAP wrapping | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Cap.Postgres` | CAP storage on Postgres | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Cap.SqlServer` | CAP storage on SQL Server | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Cap.MongoDb` | CAP storage on MongoDB | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Cap.RabbitMq` | CAP transport via RabbitMQ | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Cap.Kafka` | CAP transport via Kafka | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Cap.AzureServiceBus` | CAP transport via ASB | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq` | `AddRabbitMqEventBus` — RabbitMQ transport behind the port (native DLX/DLQ) | shipped (mono-lib) |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Kafka` | `AddKafkaEventBus` — Kafka transport (emulated DLQ) | shipped (mono-lib) |
| `WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus` | Azure.Messaging.ServiceBus direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.AzureEventHubs` | Azure.Messaging.EventHubs direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.AwsSqs` | AWSSDK.SQS direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Nats` | `AddNatsEventBus` — NATS JetStream transport (emulated DLQ, in-context settle) | shipped (mono-lib) |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Mqtt` | MQTTnet direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks` | `AddWebhooks` — outbound HMAC-signed delivery + SSRF guard + retry | shipped (mono-lib) |
| `WoW.Two.Sdk.Backend.Beta.Jobs` | Meta — Hangfire defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire` | `AddHangfireJobs(storage, opts)` + `AddInMemoryHangfireJobs()` (dev) + `UseHangfireJobsDashboard()` (local-only default). SDK serializer conventions, worker/queue tuning. **Hangfire is LGPL-3.0 — sole exception to permissive-only, per targets.md P4** | shipped |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.Postgres` | `AddPostgresHangfireJobs(connStr, opts)` — PostgreSQL storage preset | shipped |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.SqlServer` | Hangfire on SQL Server | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.Redis` | Hangfire on Redis | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Coravel` | Coravel alt | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.NCronJob` | NCronJob alt | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms` | Meta — email + SMS + push abstractions | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email` | `IEmailSender` + `EmailMessage`/`EmailSendResult` (result-typed) + `AddEmailDefaults()` (From/Reply-To defaults) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.MailKit` | `AddMailKitEmailSender()` — SMTP via MailKit (any relay; mailpit/mailhog dev) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.SendGrid` | `AddSendGridEmailSender()` — SendGrid v3 API | shipped |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.Mailgun` | Mailgun impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.Postmark` | Postmark impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.Ses` | `AddSesEmailSender()` — SES v2 simple send (no attachments; raw-MIME future) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.Acs` | Azure Communication Services email impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.FluentEmail` | FluentEmail templating | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Sms` | `ISmsSender` abstraction | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Sms.Twilio` | Twilio impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Sms.Vonage` | Vonage impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Sms.Plivo` | Plivo impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Sms.Acs` | Azure Comm Services SMS impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Push` | `IPushSender` abstraction | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Push.Fcm` | Firebase Cloud Messaging impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Push.Apns` | Apple Push (dotAPNS) impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Push.OneSignal` | OneSignal impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Push.WebPush` | Web Push impl | planned |

## P5 — SaaS-shaped

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Tenancy` | Meta — Finbuckle defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Tenancy.Finbuckle` | Finbuckle.MultiTenant wrapping | planned |
| `WoW.Two.Sdk.Backend.Beta.Tenancy.PerRow` | EF Core query-filter strategy | planned |
| `WoW.Two.Sdk.Backend.Beta.Tenancy.PerDb` | Per-tenant DbContext factory | planned |
| `WoW.Two.Sdk.Backend.Beta.Tenancy.PerSchema` | Per-tenant schema strategy | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai` | Meta — Microsoft.Extensions.AI defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.SemanticKernel` | SK wrapping | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.KernelMemory` | KernelMemory wrapping | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.OpenAi` | OpenAI provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.AzureOpenAi` | Azure OpenAI provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Anthropic` | Anthropic Claude provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Bedrock` | AWS Bedrock provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Gemini` | Google Gemini provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Ollama` | Ollama local provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Llama` | LLamaSharp local inference | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Vector.Pgvector` | pgvector + Npgsql vector store | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Vector.Redis` | Redis Stack vector store | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Vector.Qdrant` | Qdrant vector store | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Vector.Pinecone` | Pinecone vector store | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Vector.Milvus` | Milvus vector store | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Vector.Chroma` | Chroma vector store | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Vector.AzureSearch` | Azure AI Search vector store | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Tokenizers` | Microsoft.ML.Tokenizers preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Ai.Mcp` | Model Context Protocol integration | planned |
| `WoW.Two.Sdk.Backend.Beta.FeatureFlags` | Meta — Microsoft.FeatureManagement defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.FeatureFlags.OpenFeature` | OpenFeature seam | planned |
| `WoW.Two.Sdk.Backend.Beta.FeatureFlags.LaunchDarkly` | LaunchDarkly provider | planned |
| `WoW.Two.Sdk.Backend.Beta.FeatureFlags.ConfigCat` | ConfigCat provider | planned |
| `WoW.Two.Sdk.Backend.Beta.FeatureFlags.Unleash` | Unleash provider | planned |
| `WoW.Two.Sdk.Backend.Beta.FeatureFlags.GrowthBook` | GrowthBook provider | planned |
| `WoW.Two.Sdk.Backend.Beta.FeatureFlags.Esquio` | Esquio provider | planned |

## P6 — Heavy domain extensions

(Listed compactly; details TBD when phase starts.)

| Group | Packages |
|---|---|
| Realtime | `…Realtime.SignalR`, `…Realtime.SignalR.Redis`, `…Realtime.SignalR.AzureSignalR`, `…Realtime.Sse`, `…Realtime.WebSockets`, `…Realtime.MagicOnion` |
| Storage | `…Storage`, `…Storage.FluentStorage`, `…Storage.Azure`, `…Storage.S3`, `…Storage.Gcs`, `…Storage.Minio`, `…Storage.Local` |
| Media | **`…Media.Captions` — shipped** (`VttCaptionParser` + `CaptionSegment`/`CaptionTrack` + `AddVttCaptionParser()`, `src/Media/Captions/`, extracted from the TranscriptForge POC) · planned: `…Media.ImageSharp`, `…Media.Skia`, `…Media.Magick`, `…Media.FFmpeg`, `…Media.QuestPdf`, `…Media.ClosedXml`, `…Media.OpenXml`, `…Media.CsvHelper`, `…Media.Markdig` |
| Search | `…Search.PostgresFts`, `…Search.Elasticsearch`, `…Search.OpenSearch`, `…Search.Meilisearch`, `…Search.Algolia`, `…Search.Typesense`, `…Search.Lucene` |
| Workflow | `…Workflow.Stateless`, `…Workflow.Elsa`, `…Workflow.WorkflowCore`, `…Workflow.DurableTask`, `…Workflow.Temporal` |
| Aspire | `…Aspire.AppHost`, `…Aspire.ServiceDefaults`, `…Aspire.Integrations.Redis`, … (40+ Aspire integrations) |
| OIDC server | `…Identity.Server.OpenIddict`, `…Identity.Server.Duende` (commercial-aware companion) |
| GraphQL / OData | `…GraphQL.HotChocolate`, `…GraphQL.GraphQLDotNet`, `…OData` |
| Payment | `…Payments`, `…Payments.Stripe`, `…Payments.Paddle`, `…Payments.Adyen`, `…Payments.Braintree`, `…Payments.Square` |
| Geo | `…Geo.Nts`, `…Geo.Geocoding`, `…Geo.MaxMind`, `…Geo.H3` |
| Documents | `…Documents.OCR.Tesseract`, `…Documents.OCR.AzureDocumentIntelligence`, `…Documents.PdfSharp`, `…Documents.iText` |
| Tooling | `…Tools.Cli`, `…Tools.Build.Nuke`, `…Tools.SourceGen.Common` |

---

> **Maintenance**: this file is updated whenever a package is added, status changes, or a package is renamed. The single source of truth — folder structure under `src/` should match this table 1:1.
