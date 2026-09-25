# Mediator idempotency

*Last updated: 2026-09-26*

Included in `WoW2.Sdk.Backend.Beta`. Opt in with `AddMediatorDeduplicatingInterceptor()`;
requests implement `IIdempotent`. The default repository is one singleton per service provider.

## Ownership and replay

The repository atomically acquires a key and returns an ownership token. A concurrent operation
with that key receives an application conflict, without executing another handler. Only the owner
can store a replay response or release the reservation; a stale owner cannot disturb a later one.

The interceptor partitions keys by request type, server-owned tenant and principal where registered.
The caller supplies a stable operation key. Reusing it for a different payload still replays the
first response: payload fingerprinting is not provided.

Successful responses, including null references, replay for `DeduplicatingInterceptor<,>.Ttl`
(one day by default). Every SDK result carrier implements `IResult`; failures and exceptions
release ownership. Custom failure carriers must implement `IResult` too.
Caller cancellation after a successful handler does not cancel replay publication.

## Transactions

```csharp
services.AddDataSession<ProductDbContext>();
services.AddMediator(typeof(Program).Assembly);
services.AddMediatorDataUnitInterceptor();
services.AddMediatorDeduplicatingInterceptor();
```

Mark transactional commands with `ITransactionalRequest` as well as `IIdempotent`.
Data units wrap deduplication: replay is published after the root transaction commits.
Rollback releases ownership; nested completion waits for the root. A failed result is never cached.

If commit outcome is uncertain, or storing a successful replay fails, retain the reservation.
Automatically releasing it would permit repeating an effect that may already have happened.
The caller receives a conflict on retry until the owner is reconciled or this process ends.
There is no reservation expiry or administrative reconciliation API in this in-memory implementation.

## Limits and replacement

This is process-local deduplication, not crash-safe or distributed exactly-once execution.
Restart, eviction and replay expiry permit another execution. Multiple hosts need a durable
`IIdempotencyRepository` registered before the default registration. Its acquire/store/release
operations must atomically enforce the ownership token, response contract and expiry.
Durable database effects and durable replay records need their own shared transaction.

The repository contract now includes `Guid Ownership` and `ReleaseAsync`; custom implementations
must migrate all three operations. No in-workspace consumer implemented the previous contract
in the September 26 sweep.
