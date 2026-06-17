# WoW.Two.Sdk.Backend.Beta.Data.Migrations

*Last updated: 2026-06-13*

> Strategy index for the three database-migration runners shipped side-by-side in this package — `Ef/`, `DbUp/`, `Sql/`. This doc **routes**; engine internals live in each sub-folder's own lead doc (notably `Bespoke/bespoke.md`). `Sql/` is the intended default for wow-two products.

---

## The three strategies

All three register a startup runner via a descriptive `Add*` extension (no `WowTwo` prefix per `naming.md`); they differ in **who owns the schema** and **what host shapes they support**.

| Strategy | Folder | Register | Schema source-of-truth | Status |
|---|---|---|---|---|
| **EF Core** | `Ef/` | `AddEfMigrationsRunner<TContext>` | C# `DbContext` + EF migration classes (code-first) | shipped |
| **DbUp** | `DbUp/` | `AddDbUpRunner` | Embedded `.sql` scripts (DbUp journal) | shipped (legacy option) |
| **Bespoke SQL** | `Sql/` | `AddSqlMigrations` | Raw `Migrations/NNN-name/Apply.sql` pairs | DESIGN → PROVEN (smart-qr) |

---

## Comparison

| Axis | `Ef/` (`AddEfMigrationsRunner`) | `DbUp/` (`AddDbUpRunner`) | `Sql/` (`AddSqlMigrations`) |
|---|---|---|---|
| **Schema ownership** | C# code-first — `DbContext` model + EF-generated migrations own DDL | Hand-written embedded `.sql` scripts | Hand-written raw `Apply.sql`; EF (if present) is a **pure mapper** |
| **Host-agnostic** | No — coupled to `DbContext` + `Database.MigrateAsync` (startup `IHostedService` only) | Partial — `DbUpHostedService` (startup only); engine is DbUp-internal | **Yes** — one `IMigrationRunnerService` reused by CLI · HTTP endpoint · hosted-service |
| **Rollback** | EF down-migrations (rarely used) | None (roll-forward only) | Dev/test only — `Rollback.sql`, hard-gated by `MigrationOptions.AllowRollback`; prod rolls forward |
| **Multi-provider** | Any EF provider (PG/SQL Server/SQLite/…) | PG · SQL Server · MySQL via `DbUpProviderFactories` (SQLite needs custom factory) | **Postgres-only today** (advisory lock + transactional DDL); SQLite dialect is a planned drop-in seam |
| **Drift / checksum** | EF model-snapshot diff | Script journal (raw-byte hash) | Normalized SHA-256 over `Apply.sql` — no CRLF/whitespace false drift |
| **Concurrency (multi-instance)** | EF internal | None | `pg_advisory_lock` serializes the apply loop across all hosts |
| **Tooling** | `dotnet ef` | — | `wow-migrate` `dotnet tool` (`status`/`apply`/`rollback`/`new`/`promote`/`rebaseline`/`verify`/`repair`) |
| **When to pick** | Code-first shop already invested in EF migrations | Existing DbUp script library being carried forward | **Default for new wow-two products** — SQL-owned schema, host-agnostic, rebaseline/squash |

---

## Decision guide

```
New wow-two product, SQL-owned schema, want CLI + startup + host-agnostic?
    └─ Sql/  →  AddSqlMigrations   (the default)

Already committed to EF code-first; C# model is the schema truth?
    └─ Ef/   →  AddEfMigrationsRunner<TContext>

Carrying a legacy embedded-.sql DbUp script library forward?
    └─ DbUp/ →  AddDbUpRunner
```

- Greenfield → start at `Sql/`. It is the only runner that is host-agnostic (CLI today, HTTP endpoint + dev-only hosted-service later) and the only one with normalized-checksum drift detection + `pg_advisory_lock` multi-instance safety.
- `Ef/` and `DbUp/` are kept for consumers already invested in those models; they are **not** the recommended path for new products.
- One product picks **one** runner — they are mutually exclusive (each owns the schema differently). Do not register two.

### Provider note

`Ef/` and `DbUp/` are provider-flexible; `Sql/` is **Postgres-only today** by design (it issues `pg_advisory_lock` and relies on Postgres transactional DDL). If a product must target SQL Server or SQLite for schema management, use `Ef/` or `DbUp/` until the `Sql/` provider seam (a SQLite dialect) ships — see `Bespoke/bespoke.md`.

---

## Registration at a glance

```csharp
// Ef/ — code-first; applies pending EF migrations at startup for TContext
services.AddEfMigrationsRunner<AppDbContext>();
// options: EfMigrationsOptions { Enabled, MaxConnectAttempts, ConnectRetryDelay }

// DbUp/ — embedded .sql scripts; pick the engine via DbUpProviderFactories
services.AddDbUpRunner(o =>
{
    o.UpgradeEngineFactory = DbUpProviderFactories.Postgres;   // or .SqlServer / .MySql
    o.ConnectionString     = cs;
    o.ScriptsAssembly      = typeof(Program).Assembly;
});

// Sql/ — bespoke raw-SQL, host-agnostic; the intended default (see Bespoke/bespoke.md)
services.AddSqlMigrations(o => { /* MigrationOptions: AllowRollback (dev only), ... */ });
```

---

## Where things live

- `Ef/` — `EfMigrationsServiceCollectionExtensions` (`AddEfMigrationsRunner<TContext>`), `EfMigrationsHostedService<TContext>` (calls `Database.MigrateAsync`), `EfMigrationsOptions`.
- `DbUp/` — `DbUpServiceCollectionExtensions` (`AddDbUpRunner`), `DbUpHostedService`, `DbUpOptions`, `DbUpProviderFactories` (`Postgres`/`SqlServer`/`MySql`).
- `Sql/` — bespoke engine: `IMigrationRunnerService`/`MigrationRunnerService`, `MigrationDescriptor`, `AddSqlMigrations`, `MigrationOptions`, on-disk `Migrations/NNN-name/{Apply,Rollback}.sql`. **All engine internals, the `wow-migrate` CLI, packaging, and the build-then-extract plan are documented in `Bespoke/bespoke.md`** — this index does not duplicate them.

---

## See also

- `./Ef/` — EF Core code-first runner (`AddEfMigrationsRunner<TContext>`, `Database.MigrateAsync`).
- `./DbUp/` — DbUp embedded-script runner (`AddDbUpRunner`, `DbUpProviderFactories`).
- `./Bespoke/bespoke.md` — bespoke raw-SQL migrator design doc (`AddSqlMigrations`, `IMigrationRunnerService`, `MigrationDescriptor`, `Migrations/NNN-name/Apply.sql`) — engine, concurrency, CLI, packaging.
- `../Dapper/` — `IDbConnectionFactory` the `Sql/` engine runs over (BCL-only extraction pending; see `Bespoke/bespoke.md` §0.1).
- `conventions/development/backend/persistence/database.md` — schema-first rule + EF waste-rule (for `Sql/`, `Migrations/*/Apply.sql` IS the canonical schema EF maps over).
