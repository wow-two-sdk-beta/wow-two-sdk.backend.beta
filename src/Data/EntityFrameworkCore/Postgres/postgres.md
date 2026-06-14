# EF Core — Postgres (Npgsql)

> SDK-conventional Npgsql wiring: `UseNpgsqlConventional` (retry + timeout), `AddNpgsqlDataSource` (shared data source for EF + Dapper), `ApplyNpgsqlConventions` (xmin concurrency), and **`MapEnums`** (bulk enum registration via the casing engine).

Namespace: `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres`

## Bulk enum mapping — `MapEnums`

Npgsql maps native PG enums at the **data-source (driver) level**. The hand-written pattern is to list every enum twice (once on the `NpgsqlDataSourceBuilder`, once in the EF `UseNpgsql` block) — a sync hazard. `MapEnums` scans an assembly and registers them all in one call, deriving the PG type name **and** member labels from the SDK casing engine (so the driver mapping and any string-based mapping agree by construction).

```csharp
builder.Services.AddNpgsqlDataSource(b =>
    b.MapEnums(
        CaseStyle.Snake,                                   // type + member casing (default)
        ns => ns.StartsWith("MyApp.Domain"),               // optional namespace filter
        assemblies: typeof(OrderStatus).Assembly));
```

- Discovers public, non-nested enums in the given assemblies (optionally filtered by namespace).
- PG type name: `OrderStatus` → `order_status`; members: `InReview` → `in_review`.
- Per-enum name override available via the `pgTypeName` delegate.
- Member labels route through `CaseStyleNameTranslator` (the SDK engine), **not** Npgsql's built-in snake translator — one casing authority.

The same shared `NpgsqlDataSource` then drives both EF Core (`UseNpgsqlConventional(dataSource)`) and Dapper (`AddDataSourceConnectionFactory`), so enum handling is consistent across both paths.

## Other helpers

| Member | Role |
|---|---|
| `UseNpgsqlConventional(connectionString \| dataSource)` | Npgsql with 6× retry-on-failure + 30s command timeout |
| `AddNpgsqlDataSource(configure)` | shared `NpgsqlDataSource` from `DatabaseOptions.ConnectionString`, registered as `DbDataSource` too |
| `ApplyNpgsqlConventions()` (ModelBuilder) | maps the `xmin` system column as the concurrency token for every `IHasXmin` entity |

## See also

- `…Data.Dapper` — shares the `NpgsqlDataSource` via `AddDataSourceConnectionFactory`
- `…Naming` — the casing engine backing `MapEnums`
