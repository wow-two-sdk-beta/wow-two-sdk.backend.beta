# WoW.Two.Sdk.Backend.Beta.Data.Migrations

*Last updated: 2026-09-16*

> Strategy index for the three database-migration runners shipped side-by-side in this package — `Ef/`, `DbUp/`, `Bespoke/`. This doc **routes**; engine internals live in each sub-folder's own lead doc. `Bespoke/` is the intended default for wow-two products.

---

## The three strategies

All three register a startup runner via a descriptive `Add*` extension (no `WowTwo` prefix per `naming.md`); they differ in **who owns the schema** and **what host shapes they support**.

| Strategy | Folder | Register | Schema source-of-truth | Status |
|---|---|---|---|---|
| **EF Core** | `Ef/` | `AddEfMigrationsRunner<TContext>` | C# `DbContext` + EF migration classes (code-first) | shipped |
| **DbUp** | `DbUp/` | `AddDbUpRunner` | Embedded `.sql` scripts (DbUp journal) | shipped (legacy option) |
| **Bespoke SQL** | `Bespoke/` | `AddDatabaseBespokeMigrations` | Raw `Migrations/NNN-name/Apply.sql` pairs | shipped |

---

## Comparison

| Axis | `Ef/` (`AddEfMigrationsRunner`) | `DbUp/` (`AddDbUpRunner`) | `Bespoke/` (`AddDatabaseBespokeMigrations`) |
|---|---|---|---|
| **Schema ownership** | C# code-first — `DbContext` model + EF-generated migrations own DDL | Hand-written embedded `.sql` scripts | Hand-written raw `Apply.sql`; EF (if present) is a **pure mapper** |
| **Host-agnostic** | No — coupled to `DbContext` + `Database.MigrateAsync` (startup `IHostedService` only) | Partial — `DbUpBackgroundService` (startup only); engine is DbUp-internal | **Yes** — one `IMigrationRunnerService` reused by CLI · HTTP endpoint · hosted-service |
| **Rollback** | EF down-migrations (rarely used) | None (roll-forward only) | Dev/test only — `Rollback.sql`, hard-gated by `MigrationOptions.AllowRollback`; prod rolls forward |
| **Multi-provider** | Any EF provider (PG/SQL Server/SQLite/…) | PG · SQL Server · MySQL via `DbUpProviderFactory` (SQLite needs custom factory) | Postgres + SQLite; Postgres owns a whole-loop advisory lock, SQLite requires one deployment applicant |
| **Drift / checksum** | EF model-snapshot diff | Script journal (raw-byte hash) | Normalized SHA-256 over `Apply.sql` — no CRLF/whitespace false drift |
| **Concurrency (multi-instance)** | EF internal | None | Postgres: `pg_advisory_lock`; SQLite: deployment-owned single applicant (`busy_timeout` is not a mutex) |
| **Tooling** | `dotnet ef` | — | `wow-migrate` `dotnet tool` (`status`/`apply`/`rollback`/`new`/`promote`/`verify --repair`) |
| **When to pick** | Code-first shop already invested in EF migrations | Existing DbUp script library being carried forward | **Default for new wow-two products** — SQL-owned schema, host-agnostic, rebaseline/squash |

---

## Decision guide

```
New wow-two product, SQL-owned schema, want CLI + startup + host-agnostic?
    └─ Bespoke/  →  AddDatabaseBespokeMigrations   (the default)

Already committed to EF code-first; C# model is the schema truth?
    └─ Ef/   →  AddEfMigrationsRunner<TContext>

Carrying a legacy embedded-.sql DbUp script library forward?
    └─ DbUp/ →  AddDbUpRunner
```

- Greenfield → start at `Bespoke/`. It is host-agnostic and has normalized-checksum drift detection. Postgres is safe for competing runners; SQLite deployments must nominate one applicant.
- `Ef/` and `DbUp/` are kept for consumers already invested in those models; they are **not** the recommended path for new products.
- One product picks **one** runner — they are mutually exclusive (each owns the schema differently). Do not register two.

### Provider note

`Bespoke/` exposes its coordination boundary through `MigrationCoordinationMode`. PostgreSQL serializes the complete migration loop in the database. SQLite supplies transient write waiting only, so the deployment must run one migration applicant.

---

## Registration at a glance

```csharp
// Ef/ — code-first; applies pending EF migrations at startup for TContext
services.AddEfMigrationsRunner<AppDbContext>();
// options: EfMigrationsOptions { Enabled, MaxConnectAttempts, ConnectRetryDelay }

// DbUp/ — embedded .sql scripts; pick the engine via DbUpProviderFactory
services.AddDbUpRunner(cs, o =>
{
    o.UpgradeEngineFactory = DbUpProviderFactory.Postgres;   // or .SqlServer / .MySql
    o.ScriptsAssembly      = typeof(Program).Assembly;
});

// Bespoke/ — embedded raw SQL for runtime hosts; the intended default
services.AddDatabaseBespokeMigrations(typeof(Program).Assembly, options =>
{
    options.Provider = DatabaseProvider.Postgres;
});
```

---

## Where things live

- `Ef/` — `EfMigrationsServiceCollectionExtensions` (`AddEfMigrationsRunner<TContext>`), `EfMigrationsBackgroundService<TContext>` (calls `Database.MigrateAsync`), `EfMigrationsOptions`.
- `DbUp/` — `DbUpServiceCollectionExtensions` (`AddDbUpRunner`), `DbUpBackgroundService`, `DbUpOptions`, `DbUpProviderFactory` (`Postgres`/`SqlServer`/`MySql`).
- `Bespoke/` — `IMigrationRunnerService`/`MigrationRunnerService`, `MigrationDescriptor`, `AddDatabaseBespokeMigrations`, `MigrationOptions`, and `Migrations/NNN-name/{Apply,Rollback}.sql`.

---

## See also

- `./Ef/` — EF Core code-first runner (`AddEfMigrationsRunner<TContext>`, `Database.MigrateAsync`).
- `./DbUp/` — DbUp embedded-script runner (`AddDbUpRunner`, `DbUpProviderFactory`).
- `./Bespoke/bespoke.md` — bespoke raw-SQL migrator design and current guarantee record.
- `../Dapper/` — `IDbConnectionFactory` the `Sql/` engine runs over (BCL-only extraction pending; see `Bespoke/bespoke.md` §0.1).
- `conventions/development/backend/persistence/database.md` — schema-first rule + EF waste-rule (for `Bespoke/`, `Migrations/*/Apply.sql` is the canonical schema EF maps over).
