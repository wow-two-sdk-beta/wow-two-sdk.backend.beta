# WoW.Two.Sdk.Backend.Beta.Testing.Containers.RabbitMq

> RabbitMQ Testcontainers fixture.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Containers.RabbitMq
```

## Usage

```csharp
private readonly RabbitMqFixture _mq = new();

// Pass _mq.ConnectionString to your messaging registration
```
