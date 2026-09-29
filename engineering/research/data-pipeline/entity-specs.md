# Entity specs and concurrency tokens — build track

*Last updated: 2026-09-28*

> One provider-neutral description of an entity (table, keys, columns, indexes, concurrency, soft delete, tenant),
> translated by per-backend mappers (EF Core now, Dapper next, NHibernate later), plus working concurrency tokens.

## Analysis

- Concurrency today: `IHasXmin` (PostgreSQL), `IRowVersioned` (SQL Server) and `IVersioned` (portable counter) map in EF.
- `AppDbContextBase` increments `IVersioned.Version` on save; a context built on `ApplyConventions` alone does not.
- Defect (fixed in C1): Dapper `UpdateAsync` / `DeleteAsync` ignored every concurrency token; stale writes won.
- Defect (fixed in C1): Npgsql rejects `uint` parameters, so Dapper could not insert an `IVersioned` entity.
- Mapping lives in three places — EF fluent code, EF conventions over marker interfaces, and Dapper reflection over
  `IHasTableName` — so a second backend repeats every decision.

## Rulings

- `EntitySpec` is data: built by `IEntitySpecConfiguration<TEntity>` classes (like `IEntityTypeConfiguration<T>`),
  collected in `EntitySpecRegistry`, never tied to a backend.
- A mapper declares the features it supports; anything else follows `UnsupportedSpecMode` (`Throw` or `Skip`),
  set in code or host configuration (`Data:Specs:Unsupported`); the default throws, so a gap fails at startup.
- Concurrency kinds: `Counter` (portable, incremented by the SDK), `RowVersion` (SQL Server), `Xmin` (PostgreSQL),
  `Stamp` (GUID string, replaced on every write — the identity `ConcurrencyStamp`).
- A failed concurrency check raises `ConcurrencyConflictException`, mapped to `Conflict` (409).

## Status

- [x] C1 — Dapper updates and deletes check xmin, row version and counters (`ConcurrencyConflictException` → 409);
  EF and Dapper agree on the counter; 3 PostgreSQL tests
- [x] C2 — spec model + builder + registry + `UnsupportedSpecMode` (`Data:Specs:Unsupported`, default Throw)
- [x] C3 — EF mapper: tables, keys, columns, indexes, concurrency kinds by provider, stamps rotated on save,
  soft-delete filter; applied by `AppDbContextBase` to mapped entities only; 4 tests
- [x] C4 — Dapper mapper: renamed table/columns with aliased reads, ignored and store-written properties, stamp
  tokens, spec soft delete and tenant; unsupported keys follow the mode; no-spec behaviour unchanged; 2 PostgreSQL tests
