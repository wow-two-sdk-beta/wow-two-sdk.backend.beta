# Package registry

*Last updated: 2026-05-04*

> Lookup table of every NuGet package this repo produces.
> Status: **stub** = csproj exists, no impl · **scaffold** = registration + minimal API · **shipped** = real wrapper, tested · **planned** = not yet started.

## P0 — Testing scaffold (parallel track)

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Testing` | Core test host (`WowTestHost<TProgram>`), `WowApiTest<TProgram>` xUnit base, async fixture interfaces | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Assertions` | Bundles AwesomeAssertions + Shouldly | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Bogus` | `WowFaker` deterministic-seed factory | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Verify` | `WowVerifier.Initialize()` + Verify.{Xunit, AspNetCore, Http} | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.WireMock` | `WireMockFixture` (WireMock.Net async fixture) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers` | `ContainerFixtureBase<TContainer>` for engine packages | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres` | `PostgresFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Redis` | `RedisFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.RabbitMq` | `RabbitMqFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Kafka` | `KafkaFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.MongoDb` | `MongoDbFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Azurite` | `AzuriteFixture` | shipped |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Elasticsearch` | Elasticsearch Testcontainers fixture | planned |
| `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Localstack` | LocalStack (AWS emulator) fixture | planned |

## P1 — Boot floor

### Foundation

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Time` | `AddWowTwoTime`, `TimeZoneHelpers`, `CronExpressionParser`; NodaTime + Cronos + TimeZoneConverter | shipped |
| `WoW.Two.Sdk.Backend.Beta.Errors` | `WowError` record + category-to-HTTP-status mapping | shipped |
| `WoW.Two.Sdk.Backend.Beta.Results` | Re-exports ErrorOr + OneOf + Ardalis.Result | shipped |
| `WoW.Two.Sdk.Backend.Beta.Validation` | `AddWowTwoValidation` (FluentValidation assembly scan) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Serialization` | `WowJsonOptions` (camelCase + NodaTime + lenient input) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Guards` | `WowGuardExtensions` (`NotSlug`, `NotUlid`) on top of Ardalis.GuardClauses | shipped |
| `WoW.Two.Sdk.Backend.Beta.ValueObjects` | Re-exports Vogen + StronglyTypedId source generators | shipped |

### Observability

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Observability` | Meta — wires logging + tracing + metrics + health | planned |
| `WoW.Two.Sdk.Backend.Beta.Observability.Logging` | `UseWowTwoLogging` — Serilog → `ILogger<T>` w/ Console + rolling File + enrichers | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Tracing` | `AddWowTwoTracing` — OTel tracer + AspNetCore/HttpClient/Grpc/SqlClient/EFCore/Redis instrumentation | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Metrics` | `AddWowTwoMetrics` — OTel meter + AspNetCore/HttpClient/Runtime/Process | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks` | `AddWowTwoHealthChecks` — `IHealthChecksBuilder` + Xabaril provider deps (SqlServer/Postgres/MySql/Redis/RabbitMQ/Mongo/Kafka/Elastic/Network/Uris/AzureSB/AzureStorage/AwsS3/AwsSqs) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Otlp` | `AddWowTwoOtlpExporter` — OTLP gRPC/HTTP for traces + metrics | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Prometheus` | `AddWowTwoPrometheusExporter` — scrape endpoint | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.AzureMonitor` | `AddWowTwoAzureMonitorExporter` — Azure Monitor distro | shipped |
| `WoW.Two.Sdk.Backend.Beta.Observability.Datadog` | `Datadog.Trace` re-export (use OTLP route by default) | shipped |

### Web

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Web` | Meta — wires hosting + openapi + problem details + secure-headers + cors + ratelimit + outputcache | planned |
| `WoW.Two.Sdk.Backend.Beta.Web.Hosting` | `AddWowTwoHosting` / `UseWowTwoHosting` — forwarded headers + request decompression | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.OpenApi` | `AddWowTwoOpenApi` / `MapWowTwoOpenApi` — `Microsoft.AspNetCore.OpenApi` (.NET 9) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.OpenApi.Swashbuckle` | Swashbuckle fallback adapter | planned |
| `WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails` | `AddWowTwoProblemDetails` — RFC 7807 + traceId/requestId enrichment | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.RateLimit` | `AddWowTwoRateLimit` — sliding window per-IP, 100 req/min default | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.OutputCache` | `AddWowTwoOutputCache` — built-in middleware with default 60s policy | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders` | `UseWowTwoSecureHeaders` — OWASP-flavored preset (HSTS, CSP-friendly, COOP/COEP/CORP) | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.Cors` | `AddWowTwoCors` — single-line CORS policy registration | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.Compression` | `AddWowTwoCompression` — Brotli + Gzip with `Fastest` level | shipped |
| `WoW.Two.Sdk.Backend.Beta.Web.Versioning` | `AddWowTwoVersioning` — URL/header/query versioning with `1.0` default | shipped |

## P2 — Request pipeline + auth

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Mediator` | In-process MediatR-API-compat mediator (our own) | planned |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Validation` | Pipeline behavior — FluentValidation | planned |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Logging` | Pipeline behavior — request/response logging | planned |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Authorization` | Pipeline behavior — authorize per request | planned |
| `WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency` | Pipeline behavior — idempotency key | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity` | Meta — JWT + cookies + Identity API endpoints | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.Jwt` | JWT bearer wiring | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.Cookies` | Cookie auth wiring | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.Oidc` | OpenId Connect wiring | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google` | Google OAuth provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Microsoft` | Microsoft Account / Entra ID provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitHub` | GitHub OAuth provider | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Apple` | Apple Sign-In | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.IdentityApi` | `MapIdentityApi<TUser>()` defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp` | TOTP via Otp.NET | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.Mfa.WebAuthn` | FIDO2 / passkeys via Fido2NetLib | planned |
| `WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2` | Argon2 password hashing via Konscious | planned |

## P3 — Persistence + outbound

### Data

| Package | Niche | Status |
|---|---|---|
| `WoW.Two.Sdk.Backend.Beta.Data` | Meta — EF Core + audit + soft-delete + bulk + naming-conventions | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore` | EF Core base setup | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SqlServer` | SQL Server provider preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres` | Npgsql provider preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.MySql` | Pomelo MySQL provider preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite` | SQLite provider preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Cosmos` | Cosmos provider preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit` | SaveChangesInterceptor for `CreatedAt`/`UpdatedAt`/by | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete` | Query filter + restore op | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.NamingConventions` | snake_case / lower_case naming | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Bulk` | EFCore.BulkExtensions wrapper | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Triggered` | EntityFrameworkCore.Triggered preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Projectables` | EFCore.Projectables wrapper | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.Dapper` | Dapper conventions + base helpers | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef` | EF Migrations runner | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp` | DbUp script-runner alternative | planned |
| `WoW.Two.Sdk.Backend.Beta.Data.Specifications` | Ardalis.Specification wrapper | planned |

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
| `WoW.Two.Sdk.Backend.Beta.Http.Refit` | Refit registration extensions | planned |
| `WoW.Two.Sdk.Backend.Beta.Http.Resilience` | `Microsoft.Extensions.Http.Resilience` defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Http.Hedging` | Standard-Hedging handler preset | planned |
| `WoW.Two.Sdk.Backend.Beta.Http.HeaderPropagation` | Forward headers to outbound HttpClient | planned |
| `WoW.Two.Sdk.Backend.Beta.Http.Auth.OAuth2ClientCredentials` | Token-aware HttpClient handler | planned |
| `WoW.Two.Sdk.Backend.Beta.Http.Auth.Mtls` | Client cert handler | planned |

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
| `WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq` | RabbitMQ.Client direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Kafka` | Confluent.Kafka direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus` | Azure.Messaging.ServiceBus direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.AzureEventHubs` | Azure.Messaging.EventHubs direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.AwsSqs` | AWSSDK.SQS direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Nats` | NATS.Client direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Mqtt` | MQTTnet direct use | planned |
| `WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks` | Outbound webhook delivery (HMAC + retry + DLQ) | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs` | Meta — Hangfire defaults | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire` | Hangfire wrapping | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.Postgres` | Hangfire on Postgres | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.SqlServer` | Hangfire on SQL Server | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.Redis` | Hangfire on Redis | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.Coravel` | Coravel alt | planned |
| `WoW.Two.Sdk.Backend.Beta.Jobs.NCronJob` | NCronJob alt | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms` | Meta — email + SMS + push abstractions | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email` | `IEmailSender` abstraction | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.MailKit` | MailKit/MimeKit SMTP impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.SendGrid` | SendGrid impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.Mailgun` | Mailgun impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.Postmark` | Postmark impl | planned |
| `WoW.Two.Sdk.Backend.Beta.Comms.Email.Ses` | AWS SES impl | planned |
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
| Media | `…Media.ImageSharp`, `…Media.Skia`, `…Media.Magick`, `…Media.FFmpeg`, `…Media.QuestPdf`, `…Media.ClosedXml`, `…Media.OpenXml`, `…Media.CsvHelper`, `…Media.Markdig` |
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
