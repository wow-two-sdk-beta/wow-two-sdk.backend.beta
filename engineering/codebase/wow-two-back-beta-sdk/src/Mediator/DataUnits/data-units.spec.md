# Mediator data units

*Last updated: 2026-09-26*

`AddMediatorDataUnitInterceptor()` requires `AddDataSession<TContext>()`.
Only requests implementing `ITransactionalRequest` open a unit. Register exception mapping
outermost, data units next, deduplication next, and handlers last. This order lets deduplication
reserve keys inside the transaction and publish replay responses only after its commit.
Unmarked requests remain unchanged.

A successful response completes the unit. All SDK result carriers implement `IResult`;
a failed result, exception, or cancellation abandons the unit. A custom response carrier
must implement `IResult` if its returned failures should roll back.
Nested marked requests create savepoints. Their success merges into the caller's unit;
their failure rolls back only their savepoint, clears the tracker, and requires reloading
tracked references before the caller continues writing.

One root transaction is allowed per DI scope. Run independent commands in independent scopes.
This does not provide distributed idempotency or an atomic external side effect.
