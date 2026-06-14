# WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres

> PostgreSQL Testcontainers fixture.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres
```

## Usage

```csharp
private readonly PostgresFixture _pg = new();

public override async ValueTask InitializeAsync()
{
    await _pg.StartAsync();
}

protected override WebApiTestHost<Program> BuildHost() => new()
{
    ConfigureServicesHook = s =>
        s.AddDbContext<MyDb>(o => o.UseNpgsql(_pg.ConnectionString))
};

public override async ValueTask DisposeAsync()
{
    await _pg.DisposeAsync();
}
```
