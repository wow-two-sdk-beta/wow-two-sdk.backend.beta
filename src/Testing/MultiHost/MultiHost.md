# WoW.Two.Sdk.Backend.Beta.Testing.MultiHost

> Boot **several in-process web hosts over one set of shared backing services** in a single
> integration-test fixture — e.g. a management API + a redirect API sharing one Postgres container.
> Single-host tests stay on `WebApiTestBase<T>`; reach for this only for "two services, one database".

Part of the `WoW.Two.Sdk.Backend.Beta.Testing` package — no extra install.

## What `MultiHostFixture` orchestrates

One shared lifecycle for the whole test collection:

1. **Start** shared fixtures (containers) via the composed `IAsyncFixtureCollection`.
2. **Inject** env/config so every host targets them — `ConfigureEnvironment()`.
3. **Build** every host; the first build runs the startup migrations, the rest see the migrated schema.
4. **Initialize state** after migrations — `InitializeStateAsync()` (e.g. snapshot for reset).
5. **Reset** per test → delegates to the shared collection's `ResetAsync()` (the `IAsyncTestFixture` contract only — never a container's internals).
6. **Dispose** hosts, then shared fixtures (reverse order).

Decoupled from any concrete fixture: provider-specific post-migration wiring (e.g. Respawn snapshot) lives in the app's `InitializeStateAsync` override, where the concrete fixture type is in scope.

## `extern alias` — required for two `Program`s

Two minimal-hosting apps each emit `Program` in their assembly's global namespace → ambiguous in one test project. Alias each app project and bind at the top of the fixture:

```xml
<!-- test .csproj -->
<ProjectReference Include="..\App.Api\App.Api.csproj"           Aliases="apihost" />
<ProjectReference Include="..\App.Redirect\App.Redirect.csproj" Aliases="redirecthost" />
```

```csharp
extern alias apihost;
extern alias redirecthost;

using ApiProgram      = apihost::Program;
using RedirectProgram = redirecthost::Program;
```

A single host needs no alias.

## Subclassing

```csharp
extern alias apihost;
extern alias redirecthost;

using ApiProgram      = apihost::Program;
using RedirectProgram = redirecthost::Program;
using WoW.Two.Sdk.Backend.Beta.Testing.Containers;
using WoW.Two.Sdk.Backend.Beta.Testing.MultiHost;

public sealed class AppFixture : MultiHostFixture
{
    private readonly PostgresFixture _pg;

    public AppFixture()
    {
        _pg          = AddSharedFixture(new PostgresFixture());
        ApiHost      = AddHost(new WebApiTestHost<ApiProgram>());
        RedirectHost = AddHost(new WebApiTestHost<RedirectProgram>());
    }

    public WebApiTestHost<ApiProgram> ApiHost { get; }
    public WebApiTestHost<RedirectProgram> RedirectHost { get; }

    // (2) point every host at the shared DB before they build — env overlay is the authoritative seam
    protected override void ConfigureEnvironment() =>
        Environment.SetEnvironmentVariable("APP_DB_CONNECTION", _pg.ConnectionString);

    // (4) runs AFTER the hosts migrated — snapshot the real schema for the per-test reset
    protected override async ValueTask InitializeStateAsync(CancellationToken ct = default) =>
        await _pg.InitializeRespawnerAsync(ct);
}
```

xUnit wiring (shared once per collection, reset per test):

```csharp
[CollectionDefinition(AppCollection.Name)]
public sealed class AppCollection : ICollectionFixture<AppFixtureLifetime>
{
    public const string Name = "app-e2e";
}

// adapt MultiHostFixture's ValueTask lifecycle to xUnit's IAsyncLifetime
public sealed class AppFixtureLifetime : AppFixture, IAsyncLifetime
{
    public async Task InitializeAsync() => await StartAsync();
    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();
}

[Collection(AppCollection.Name)]
public abstract class E2EBase(AppFixtureLifetime fixture) : IAsyncLifetime
{
    protected AppFixtureLifetime Fixture { get; } = fixture;
    public async Task InitializeAsync() => await Fixture.ResetAsync(); // wipe data per test
    public Task DisposeAsync() => Task.CompletedTask;
}
```

Domain-specific request builders (e.g. a `CodeRequests` helper) stay app-side — they're not shipped here.

## See also

- [Testing.md](../Testing.md) — single-host `WebApiTestBase<T>`
- [Containers.md](../Containers/Containers.md) — the `IAsyncTestFixture` container fixtures composed in
- `Polling.UntilAsync` — poll an eventually-consistent probe (e.g. async analytics flush) across hosts
