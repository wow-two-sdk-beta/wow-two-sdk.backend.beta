# Naming conventions

*Last updated: 2026-06-14*

## Package IDs

`WoW.Two.Sdk.Backend.Beta.<Area>[.<SubArea>]`

Examples:

- `WoW.Two.Sdk.Backend.Beta` — meta-package
- `WoW.Two.Sdk.Backend.Beta.Time`
- `WoW.Two.Sdk.Backend.Beta.Caching`
- `WoW.Two.Sdk.Backend.Beta.Caching.Redis`
- `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google`
- `WoW.Two.Sdk.Backend.Beta.Testing`
- `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres`

PascalCase. Dotted. No abbreviations except established ones (e.g., `OAuth`, `Sql`, `Http`).

## Folder names (filesystem)

`PascalCase`. The folder name matches the namespace / package-id segment **1:1** — same casing, no hyphens, no underscores. So a file's `namespace` is its folder path under `src/` verbatim. This keeps `CA1707` (underscores in identifiers) impossible at the root and removes any need for `.editorconfig` suppression.

- `src/Foundation/Time/` → `WoW.Two.Sdk.Backend.Beta.Foundation.Time`
- `src/Web/OutputCache/` → `WoW.Two.Sdk.Backend.Beta.Web.OutputCache`
- `src/Identity/OAuth/Google/` → `WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google`

Apply the §Acronyms rules to each segment (`ai` → `AI`, `auth-oauth2-client-credentials` → `AuthOAuth2ClientCredentials`, `hangfire-postgres` → `HangfirePostgres`, `feature-flags` → `FeatureFlags`), and keep established product casings (`MailKit`, `SendGrid`, `OpenAI`, `Postgres`, `SqlServer`, `RabbitMq`).

> **Linux CI is case-sensitive.** Build-file path globs (`DefaultItemExcludes`, `.slnx` project paths, workflow `dotnet pack` paths) must match the on-disk PascalCase exactly — a lowercased path silently no-ops on `ubuntu-latest` even though macOS hides it.

## Namespaces

Match the package id 1:1. File-scoped:

```csharp
namespace WoW.Two.Sdk.Backend.Beta.Time;
```

## Public extension classes

`<Area>ServiceCollectionExtensions` — matches Microsoft's pattern.

```csharp
public static class TimeServiceCollectionExtensions
{
    public static IServiceCollection AddTimeProviders(this IServiceCollection services, ...) { ... }
}
```

For non-`IServiceCollection` extensions, name `<Target>Extensions` or `<Area><Target>Extensions`:

- `TimeProviderExtensions` (extends `TimeProvider`)
- `LoggingBuilderExtensions` (extends `ILoggingBuilder`)
- `OpenTelemetryBuilderExtensions` (extends `IOpenTelemetryBuilder`)
- `EndpointConventionBuilderExtensions` (extends `IEndpointConventionBuilder`)

## Registration method names

**No `WoWTwo` prefix on method names.** The package name (`WoW.Two.Sdk.Backend.Beta.<Area>`) carries the brand; method names describe what the registration concretely does.

This mirrors the older `Backbone.Language.Features.Serialization` package convention where the method is `AddSystemTextJsonSerializer` — describing precisely what gets registered, not who built the wrapper.

### Examples

```csharp
// Foundation
services.AddTimeProviders();
services.AddFluentValidatorsFromAssemblies(typeof(Program).Assembly);

// Observability
host.UseSerilogConventional();
services.AddOpenTelemetryTracing("my-service");
services.AddOpenTelemetryMetrics("my-service");
services.AddOtlpExporters(new Uri("http://collector:4317"));
services.AddPrometheusMetricsExporter();
services.AddAzureMonitorExporter();
services.AddHealthChecksBuilder();

// Web
services.AddProxyAwareHosting();
app.UseProxyAwareHosting();
services.AddOpenApiDefaults();
app.MapOpenApiEndpoint();
services.AddTraceAwareProblemDetails();
services.AddPerIpSlidingWindowRateLimit();
services.AddDefaultOutputCache();
app.UseOwaspSecureHeaders();
services.AddDefaultCorsPolicy("https://example.com");
services.AddBrotliGzipCompression();
services.AddDefaultApiVersioning();

// Mediator
services.AddMediator(typeof(Program).Assembly);
services.AddMediatorBehavior(typeof(LoggingBehavior<,>));
services.AddMediatorValidationBehavior();
services.AddMediatorLoggingBehavior();
services.AddMediatorAuthorizationBehavior();
services.AddMediatorIdempotencyBehavior();

// Identity
services.AddJwtBearerAuthentication(o => { ... });
services.AddCookieAuthentication();
services.AddOpenIdConnectAuthentication(o => { ... });
services.AddIdentityApiEndpoints<AppDb>();
authBuilder.AddGoogleAuthentication(clientId, clientSecret);
authBuilder.AddMicrosoftAuthentication(clientId, clientSecret);
authBuilder.AddGitHubAuthentication(clientId, clientSecret, scopes);
authBuilder.AddAppleAuthentication(clientId, teamId, keyId, keyPath);
services.AddFido2WebAuthn(domain, name, origins);
services.UseArgon2PasswordHasher<IdentityUser>();
```

### Naming patterns

| Pattern | Example | When to use |
|---|---|---|
| `Add<Concrete>` | `AddJwtBearerAuthentication`, `AddOpenTelemetryTracing` | Scheme/system-specific registration |
| `Add<Default><Thing>` | `AddDefaultCorsPolicy`, `AddDefaultOutputCache` | Pre-set policy/configuration |
| `Add<Specific><Thing>` | `AddPerIpSlidingWindowRateLimit`, `AddBrotliGzipCompression` | Picks one strategy among many |
| `Use<Concrete>` | `UseOwaspSecureHeaders`, `UseSerilogConventional` | Pipeline middleware |
| `Map<Endpoint>` | `MapOpenApiEndpoint` | Endpoint routing |
| `Add<Lib>FromAssemblies` | `AddFluentValidatorsFromAssemblies` | Assembly-scanning registration |

**Rule of thumb**: a developer who has never heard of wow-two should be able to read the method name and immediately know what it does. If you'd want to add a comment like `// these are wow-two's defaults`, the method name is wrong — bake the meaning in.

### Data / persistence verbs (ecosystem-wide)

One vocabulary for store operations, **identical across every provider** (EF Core, Dapper, SqlServer/Npgsql/Sqlite/Mongo/in-memory). The same call reads the same regardless of backend — that's the point.

| Verb | Means | Not |
|---|---|---|
| `Create` | bring a **new** entity into existence (persist a new row) | `Insert` (SQL-specific), `Add` (reserved — see below) |
| `Get` | read. Composes: `GetById`, `GetAll`, `GetByFilter` | `List` (breaks at `ListById`), `Fetch` (implies remote) |
| `Update` | persist changes to an existing entity | |
| `Delete` | remove. Plus id form `DeleteById` | `Remove` |

`Add` is **reserved for membership / collection operations** — `AddUserToGroup`, add-to-in-memory-collection, `services.Add…` DI registration. It never means "persist a new row" (that's `Create`).

Method shape: `{Verb}[By{Key}][Range]Async`, always `Async`-suffixed, `CancellationToken cancellationToken = default` last. Examples: `CreateAsync`, `CreateRangeAsync`, `GetByIdAsync`, `DeleteByIdAsync`.

> **Why not the Specification pattern / Ardalis repo?** It's `IQueryable`-bound and can't lower to Dapper SQL — it would split the query story across providers, the exact inconsistency this vocabulary avoids. Thin repos own id-based CRUD; complex queries are plain LINQ (EF) or `SqlNaming`-built SQL (Dapper) where actually needed.

### Stable identifiers (cookie names, policy names)

Constants that need to be unique system-wide may keep a stable identifier shape (e.g. cookie name `.app.auth`, policy name `"default"`). Don't bake `wow-two` into them — they're consumer-visible and may collide with consumer-defined names.

### `ActivitySource` / `Meter` names

Internal diagnostics types use `WoW.Two.<Area>` — these are intentional brand prefixes so trace/metric filtering works at scale across services. **Different from public method names.**

## Options types

`<Area>Options` — record, init-only.

```csharp
public sealed record TimeOptions
{
    public string TimeZone { get; init; } = "UTC";
}
```

For sub-areas:

```csharp
public sealed record CachingRedisOptions { ... }
```

## Internal types

Plain PascalCase. No leading underscore. Live in an `Internal/` folder when the namespace becomes crowded.

```csharp
internal sealed class TimeProviderRegistrar { ... }
```

## Test files

`<Module>.tests.cs` next to the file under test. Class name `<Module>Tests`.

```
TimeModule.cs
TimeModule.tests.cs   // class TimeModuleTests
```

## Spec / standard files

- `<Module>.standard.md` — RFC 2119 contract
- `<Module>.spec.md` — API surface + usage

## Symbols banned in our code

- `var` for non-obvious types (let analyzers complain)
- Hungarian notation (`m_`, `s_`, `_`)
- Suffix `Helper`, `Util`, `Manager` for *public* types — internal helpers ok
- `dynamic` (use generics or polymorphism)
- `BinaryFormatter` (security)

## Acronyms

Pascal-case all acronyms longer than 2 letters: `OpenApi`, `JsonSchema`, `OAuth` (special — established).

Two-letter acronyms stay all-caps: `IO`, `UI`, `ID` (but `Id` when used as a member name like `UserId`).

## Database / table names (when our packages create schema)

Snake_case (Postgres convention via `EFCore.NamingConventions`). Schema named `wow_two_<area>` (e.g., `wow_two_outbox.outbox_messages`).
