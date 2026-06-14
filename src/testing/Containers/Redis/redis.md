# WoW.Two.Sdk.Backend.Beta.Testing.Containers.Redis

> Redis Testcontainers fixture.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Containers.Redis
```

## Usage

```csharp
private readonly RedisFixture _redis = new();

protected override WebApiTestHost<Program> BuildHost() => new()
{
    ConfigureServicesHook = s =>
        s.AddStackExchangeRedisCache(o => o.Configuration = _redis.ConnectionString)
};
```
