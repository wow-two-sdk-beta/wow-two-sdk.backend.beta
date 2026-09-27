# Transaction locks

> Exclusive per-key locks that live exactly as long as the current EF Core transaction.

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);
await db.Database.AcquireTransactionLocksAsync("owner", [ownerId], ct);
if (await db.Codes.CountAsync(code => code.OwnerId == ownerId, ct) < cap)
{
    db.Codes.Add(code);
    await db.SaveChangesAsync(ct);
}
await transaction.CommitAsync(ct);
```

- Serialize a check-then-write invariant, such as a plan cap, across hosts and connections.
- Name keys by subject (`owner:{id}`); every writer of the invariant must take the same keys.
- Keys hash to 64 bits and lock in ascending order, so two callers never deadlock on key order.
- PostgreSQL holds `pg_advisory_xact_lock` until commit or rollback. SQLite takes no lock: it serializes writers.
- An absent transaction throws `InvalidOperationException`; other providers throw `NotSupportedException`.
- Inside a data session the unit's transaction is current, so the same call applies.
