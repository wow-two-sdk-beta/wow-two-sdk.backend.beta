# Unique saves

> Unique-constraint conflicts as results, inside a transaction that stays usable.

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);
string? slug = await db.SaveWithGeneratedKeyAsync(
    code,
    applyKey: (entity, key) => entity.Slug = key,
    nextKey: () => ids.Generate(7, IdAlphabetConstants.Alphanumeric),
    maxAttempts: 5,
    constraintName: "ix_codes_slug",
    ct);
if (slug is null)
    return AppError.Of(AppErrorType.Conflict, "A unique link could not be allocated. Try again.");
await transaction.CommitAsync(ct);
```

- `TrySaveChangesUniqueAsync` saves under a savepoint and returns `false` on the named unique constraint.
- A conflict rolls back to the savepoint, detaches the entities being added and leaves the transaction usable.
- Any other failure still throws, including a unique violation on a different constraint.
- `SaveWithGeneratedKeyAsync` bounds the retries, so a saturated key space ends in `null`, not a spin.
- The database constraint stays authoritative; no exists-then-insert probe runs.
- SQLite reports no constraint name: there, any unique or primary-key violation counts.
