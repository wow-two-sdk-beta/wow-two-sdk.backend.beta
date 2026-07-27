# WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency

> Pipeline behavior — dedupes requests marked with `IIdempotent` via a pluggable `IIdempotencyStore`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency
```

## Usage

```csharp
builder.Services.AddMediator(typeof(Program).Assembly);
builder.Services.AddMediatorIdempotencyBehavior();   // in-memory store
```

Mark a request:

```csharp
public sealed record CreateOrder(string IdempotencyKey, decimal Amount, ...) : IRequest<OrderId>, IIdempotent;
```

The first call executes the handler; subsequent calls with the same `IdempotencyKey` return the cached response (24h TTL by default — set via `IdempotencyBehavior<,>.Ttl`).

## Distributed scenarios

The default store is in-process — replace with a Redis/SQL implementation:

```csharp
public sealed class RedisIdempotencyStore(IConnectionMultiplexer redis) : IIdempotencyStore { ... }

builder.Services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
```
