# WoW.Two.Sdk.Backend.Beta.Testing

> Core test host (`WebApiTestHost<T>`), `WebApiTestBase<T>` xUnit base, and async fixture interfaces.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing
```

## Usage

### Inheriting `WebApiTestBase<TProgram>`

```csharp
using WoW.Two.Sdk.Backend.Beta.Testing;

public class GreetingsTests : WebApiTestBase<Program>
{
    [Fact]
    public async Task Get_returns_hello()
    {
        var resp = await Client.GetStringAsync("/hello");
        Assert.Equal("Hello", resp);
    }

    [Fact]
    public async Task Time_advances_via_fake_clock()
    {
        Clock.Advance(TimeSpan.FromHours(1));
        // ... time-sensitive assertions
    }
}
```

### Composing with container fixtures

```csharp
public class PaymentTests : WebApiTestBase<Program>
{
    private readonly PostgresFixture _pg = new();

    public override async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override WebApiTestHost<Program> BuildHost() => new()
    {
        ConfigureServicesHook = services =>
        {
            services.AddDbContext<MyDb>(o => o.UseNpgsql(_pg.ConnectionString));
        }
    };
}
```

## Spec

- [Standard](Testing.standard.md)
- [Spec](Testing.spec.md)

## See also

- `Microsoft.AspNetCore.Mvc.Testing` (the underlying lib)
- `Microsoft.Extensions.TimeProvider.Testing` for `FakeTimeProvider`
- `Respawn` for DB reset (transitively included)
