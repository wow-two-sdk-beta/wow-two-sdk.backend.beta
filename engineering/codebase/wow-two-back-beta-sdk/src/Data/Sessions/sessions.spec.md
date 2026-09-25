# Data sessions

*Last updated: 2026-09-26*

## Activation

`AddDataSession<TContext>()` selects one already registered relational `DbContext` per DI scope.
Resolution is lazy with respect to the database connection. Root resolution and retrying execution
strategies are rejected. The context still owns entity tracking and `SaveChangesAsync`.
The session owns one explicit transaction; no default host registration enables it.

## Units

`await using var unit = await session.BeginAsync(ct)` starts a transaction. `CompleteAsync`
flushes pending EF changes and commits; disposal without completion rolls back using a separate
cleanup cancellation budget. A completed or failed session cannot start another transaction.
Open another DI scope for another transaction. Operations are sequential, like `DbContext`.
An external transaction on the selected context is rejected rather than silently adopted.

Nested units flush the parent's pending changes before creating a savepoint. Completion releases
only that savepoint. Abandonment rolls back to it and clears the EF tracker: old object references
must be reloaded before further writes. Parent database writes and parent hooks survive.
This explicit recovery contract replaces automatic object-graph snapshot restoration, which
cannot safely reconstruct arbitrary owned graphs, navigation collections or external references.
Units settle in reverse opening order. Nesting is bounded by `MaxDepth`.

## Connections

`OpenConnectionAsync(factory, ct)` returns a disposable `DataConnectionLease`. Idle sessions use
an owned factory connection. Active sessions lend their EF connection and transaction; disposal
releases the lease without closing either. Completion with an outstanding lease is rejected.
Factories targeting a different connection type or provider-normalized settings are refused inside a unit.
Passwords are excluded from comparison because pooled sources redact them; the authenticated EF
connection supplies credentials. Database, principal and other connection settings must still match.
Raw SQL must pass the lease's transaction. Hand-written factory calls remain autonomous by design.
Migrations continue using their singleton factory and never resolve the session.

## Hooks

`OnCommittedAsync(action, key)` queues an action in the current unit; with an idle session it runs
immediately. Register after a successful write. It is not a SaveChanges interceptor: EF automatic
savepoints do not collect actions, and callers must not register success actions before writes.
Nested rollback discards its commit actions and runs its rollback actions. Nested completion moves
both lists to the parent. A repeated key retains the shallowest surviving commit action.
`OnRolledBack(action)` registers compensation for the current explicit unit.

Hooks run after successful commit or rollback, each with its own timeout and isolated exception handling.
Hook failures are logged and counted; they never report a committed transaction as rolled back.
Callbacks must honor cancellation. They are in-process notifications without crash durability or
cross-resource atomicity; durable external effects belong in the transactional outbox.
Callbacks cannot reenter the same session. A callback ignoring cancellation may continue after
its timeout, so it must not retain scoped database objects.

An exception from database commit makes the outcome uncertain. Cleanup discards both callback
lists without claiming rollback; the session stays faulted. Reconcile database state before retrying.
Idempotency reservations deliberately remain held in this case. Disposing the DI scope does not
turn a missing commit acknowledgment into evidence that the database rolled back.

## Usage

```csharp
services.AddDataSession<ProductDbContext>();

await using var unit = await session.BeginAsync(ct);
await writeRepository.CreateAsync(entity, ct);
await using (var lease = await session.OpenConnectionAsync(connectionFactory, ct))
{
    await lease.Connection.ExecuteAsync(new CommandDefinition(
        sql, parameters, transaction: lease.Transaction, cancellationToken: ct));
}
await session.OnCommittedAsync(token => cache.RemoveAsync(cacheKey, token), cacheKey);
await unit.CompleteAsync(ct);
```

Full-row Dapper-to-EF materialization, graph merging, manual repository flush mode, cache adapters,
and arbitrary external savepoints are outside this contract. The larger data-vector design retains
those obligations separately; this API establishes the transactional foundation.
Session lifetime checks cover session APIs and participating Dapper calls; they do not intercept
direct DbContext writes after completion. Generic Dapper writes still lack concurrency predicates;
`xmin` materialization supports scalar Dapper reads followed by EF concurrency-checked writes.

Mediator composition is documented in [data units](../../Mediator/DataUnits/data-units.spec.md).
