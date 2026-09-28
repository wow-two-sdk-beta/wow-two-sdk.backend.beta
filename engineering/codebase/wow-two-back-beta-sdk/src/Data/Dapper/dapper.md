# Dapper

*Last updated: 2026-09-26*

Included in `WoW2.Sdk.Backend.Beta`. `AddDapperConventions()` configures underscore matching,
`DateOnly` and list type handlers. `SqlNamingOptions` controls generated identifiers per repository.
Register global type handlers before queries; do not change them per request.

## Connections and units

Register a shared `DbDataSource` with `AddDataSourceConnectionFactory()`.
Independent operations open and dispose factory connections. Optional `AddDataSession<TContext>()`
lets DI-created repositories borrow the active EF connection and transaction.
Hand-written SQL participates explicitly:

```csharp
await using var lease = await session.OpenConnectionAsync(connectionFactory, ct);
var rows = await lease.Connection.QueryAsync<Channel>(
    new CommandDefinition(sql, parameters, transaction: lease.Transaction, cancellationToken: ct));
```

Direct factory calls remain autonomous. Dispose a lease before completing its unit.
See [data sessions](../Sessions/sessions.spec.md) for nesting and rollback recovery.

## Generated CRUD

`AddDapperRepository<TEntity, TId>()` registers single-table operations for entities implementing
`IKeyedEntity<TId>` and `IHasTableName`. Ambient tenants scope generated CRUD; no ambient tenant
means an explicit unscoped system/admin operation.

All generated reads exclude `ISoftDeletable.IsDeleted` rows. A specialized administrative repository
can override `IncludeSoftDeleted`. This is a read policy; generated deletes remain physical deletes.
Custom SQL owns equivalent tenant and soft-delete predicates.

PostgreSQL `IHasXmin` reads explicitly select `xmin`. Insert/update property lists omit that
store-generated column. Insert does not refresh its value: re-read before EF attachment.
The scalar Dapper-read → attach unchanged → mutate → EF-save path is tested against PostgreSQL.

Generated `UpdateAsync` and `DeleteAsync(entity)` check the entity's concurrency token and raise
`ConcurrencyConflictException` (mapped to 409) when another writer got there first:

| Token | Update | Delete |
|---|---|---|
| `IVersioned` | `WHERE version = @Version`, sets `version + 1`; the entity's `Version` follows | same check |
| `IHasXmin` | `WHERE xmin = …`, `RETURNING xmin`; the entity's `Xmin` is refreshed | same check |
| `IRowVersioned` | `WHERE row_version = @RowVersion`, `OUTPUT INSERTED.row_version` (SQL Server) | same check |

`DeleteByIdAsync(id)` carries no token and stays unchecked. Unsigned columns (`uint` tokens) bind as
`long`, since neither Npgsql nor SqlClient accepts unsigned parameters.

This remains a reflection-based single-table repository. EF value converters, owned graphs,
partial projections, row locking and full-row provenance are not inferred from the EF model.
Use bespoke SQL or EF for those contracts.
