# Testing — spec

*Last updated: 2026-05-04*

## NuGet

```
WoW.Two.Sdk.Backend.Beta.Testing
```

## Public API

### `WebApiTestHost<TEntryPoint>`

| Member | Type | Notes |
|---|---|---|
| `Clock` | `FakeTimeProvider` | Mutable virtual clock. |
| `ConfigureServicesHook` | `Action<IServiceCollection>?` | Init-only; runs in `ConfigureWebHost.ConfigureServices`. |
| `ConfigureHostHook` | `Action<IHostBuilder>?` | Init-only; runs in `CreateHost`. |

### `WebApiTestBase<TEntryPoint>`

| Member | Type | Notes |
|---|---|---|
| `Host` | `WebApiTestHost<TEntryPoint>` | Lazy. |
| `Client` | `HttpClient` | Calls `Host.CreateClient()` per access. |
| `Clock` | `FakeTimeProvider` | Forwarded from `Host.Clock`. |
| `BuildHost()` | virtual `WebApiTestHost<TEntryPoint>` | Override to compose fixtures. |
| `InitializeAsync()` | virtual `ValueTask` | Override for per-test setup. |
| `DisposeAsync()` | virtual `ValueTask` | Override for per-test teardown. |

### `IAsyncTestFixture`

| Member | Type |
|---|---|
| `Name` | `string` |
| `StartAsync(CancellationToken)` | `ValueTask` |
| `ResetAsync(CancellationToken)` | `ValueTask` |
| `DisposeAsync()` | `ValueTask` (from `IAsyncDisposable`) |

### `IAsyncFixtureCollection : IAsyncTestFixture`

| Member | Type |
|---|---|
| `Fixtures` | `IReadOnlyCollection<IAsyncTestFixture>` |

### `AsyncFixtureCollection : IAsyncFixtureCollection`

| Member | Notes |
|---|---|
| `()` ctor | Empty |
| `(IEnumerable<IAsyncTestFixture>)` ctor | Seeded |
| `Add(IAsyncTestFixture)` | Returns `this` for chaining |

## Usage

### Plain integration test

```csharp
public class HelloTests : WebApiTestBase<Program>
{
    [Fact]
    public async Task Greets() =>
        Assert.Equal("hi", await Client.GetStringAsync("/hi"));
}
```

### With container fixture

```csharp
public class OrderTests : WebApiTestBase<Program>
{
    private readonly PostgresFixture _pg = new();

    public override async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await base.InitializeAsync();
    }

    protected override WebApiTestHost<Program> BuildHost() => new()
    {
        ConfigureServicesHook = s => s.AddDbContext<OrdersDb>(o => o.UseNpgsql(_pg.ConnectionString))
    };

    public override async ValueTask DisposeAsync()
    {
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

### Time control

```csharp
[Fact]
public async Task Token_expires_after_ttl()
{
    var token = await IssueToken();
    Clock.Advance(TimeSpan.FromMinutes(31));
    var resp = await Client.GetAsync("/with-token");
    Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
}
```

## See also

- [Testing.standard.md](./Testing.standard.md)
- [testing.md](./testing.md)
