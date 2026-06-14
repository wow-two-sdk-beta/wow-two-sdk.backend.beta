# WoW.Two.Sdk.Backend.Beta.Testing.Containers

> Base for Testcontainers-backed fixtures. Per-engine packages (Postgres, Redis, RabbitMq, etc.) extend `ContainerFixtureBase<TContainer>`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Containers
```

You typically don't depend on this directly — use the engine-specific packages instead:

- `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres`
- `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Redis`
- `WoW.Two.Sdk.Backend.Beta.Testing.Containers.RabbitMq`
- `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Kafka`
- `WoW.Two.Sdk.Backend.Beta.Testing.Containers.MongoDb`
- `WoW.Two.Sdk.Backend.Beta.Testing.Containers.Azurite`

## Subclassing

```csharp
public sealed class PostgresFixture : ContainerFixtureBase<PostgreSqlContainer>
{
    public PostgresFixture() : base(new PostgreSqlBuilder().Build()) { }
    public override string Name => "postgres";
    public string ConnectionString => Container.GetConnectionString();
}
```

## See also

- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
