# WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke

> Bespoke, host-agnostic SQL migrator — raw `.sql` pairs, normalized-checksum tracking, Postgres transactional DDL + advisory lock. One engine, three hosts (CLI now · HTTP endpoint later · hosted-service dev-only). Roll-forward in prod, rollback in dev/test only.

*Last updated: 2026-06-13*

> **Living design doc.** Status lifecycle: **DESIGN → PROVEN (smart-qr) → SHIPPING → STABLE**. Current: **SHIPPING** — the engine now **lives in this folder** (`src/Data/Migrations/Bespoke/`, 22 `.cs` files), extracted from `smart-qr` on 2026-06-13 and building green in the mono-lib. It was first **PROVEN (smart-qr · 2026-06-11)** — built greenfield + verified end-to-end against real Postgres in the `smart-qr` product (see §0.0). `smart-qr` stays untouched and becomes a consumer of this package post-publish.
> The engine was **built greenfield in `smart-qr`** and proven against the real product (see §0.0), then **extracted here** (see §6 — a *build-then-extract*, not a file move). The one real code change at extraction was the connection seam (below). This doc is **updated each iteration** as the engine, CLI, and conventions firm up.
> Sits beside the existing `Ef/` and `DbUp/` runners (both shipped as folders in the mono-lib) as the third — and intended default — strategy. The strategy index that routes Ef vs DbUp vs Sql is `../migrations.md`.

> **Extraction landed (2026-06-13).** 22 engine files copied into `src/Data/Migrations/Bespoke/`; namespace rewritten `SmartQr.Common.Persistence.Migrations` → `WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke`. **Connection seam re-pointed** to the SDK's `IDbConnectionFactory` (`src/Data/Dapper/IDbConnectionFactory.cs`, `CreateOpenAsync` → `ValueTask<DbConnection>`, `Create` → `DbConnection`): the injected factory is now `IDbConnectionFactory`, and connection/transaction params across `MigrationRunnerService`, `IMigrationHistoryRepository`/`MigrationHistoryRepository`, and `IMigrationDialect`/`PostgresMigrationDialect` are now BCL `System.Data.Common.DbConnection` / `DbTransaction`. **No `DbConnection → NpgsqlConnection` downcast was required** — every connection call is Dapper (`ExecuteAsync`/`QueryAsync`) or BCL `DbConnection.BeginTransactionAsync`, all of which work over `DbConnection`. The single **genuinely Npgsql-typed site** is `PostgresMigrationDialect.EnsureDatabaseExistsAsync`, which constructs its **own** `NpgsqlConnection`/`NpgsqlCommand`/`NpgsqlConnectionStringBuilder` from a connection string (the maintenance-DB `CREATE DATABASE` step) — that is the documented provider-leak point (§2.1.1), self-contained and untouched. Real shipped type names: `IMigrationRunnerService`/`MigrationRunnerService`, `IMigrationHistoryRepository`/`MigrationHistoryRepository`, `IMigrationScanner`/`MigrationScannerService`, `IMigrationSource` (+ `FileSystemMigrationSource`, `EmbeddedResourceMigrationSource`), `IMigrationDialect`/`PostgresMigrationDialect`, `MigrationDescriptor`, `MigrationHistoryEntry`, `MigrationStatus`, `MigrationOptions`, `DatabaseProvider`, `RawMigration`, `MigrationDriftException`, `MigrationOrphanException`, `MigrationChecksumExtensions`, `MigrationConventions`, and the `AddDatabaseBespokeMigrations` registration (`MigrationServiceCollectionExtensions`). **Not yet here:** the CLI host, the `IHostedService` adapter, and the `…Data.Abstractions` BCL split (§0.1) — the engine currently rides the mono-lib's transitive `Npgsql` (via the EF provider) + `Dapper`, and web-freedom is not yet enforced. Three SDK-only analyzer fixes were applied during extraction (the SDK runs `TreatWarningsAsErrors`, smart-qr did not): CA1859 on `EmbeddedResourceMigrationSource.GetRollbackOrThrow` (param `IReadOnlyDictionary` → `Dictionary`) and on `CreateDialect` (return `IMigrationDialect` → concrete `PostgresMigrationDialect`, registered as `services.AddSingleton<IMigrationDialect>(…)`), and CA1310 on `MigrationScannerService` (`StartsWith("--")` → `StartsWith("--", StringComparison.Ordinal)`).

---

## 0.0 Proven in smart-qr (2026-06-11)

First iteration shipped in `smart-qr-poc` — engine in `SmartQr.Common.Persistence/Migrations/`, CLI in `SmartQr.Migrations.Cli/`. The trigger was a runtime 500 (`column "user_id" of relation "codes" does not exist`) after an `owner→user` rename: EF `EnsureCreated` never alters, so the schema went stale. Replacing it with this migrator fixed it.

- **Engine** (21 files, after the 2026-06-12 rename polish): `IMigrationSource` (`FileSystemMigrationSource` + `EmbeddedResourceMigrationSource`), `IMigrationScanner`/`MigrationScannerService` (`NNN-name` + normalized SHA-256 + `-- @no-transaction`), `IMigrationHistoryRepository`/`MigrationHistoryRepository` (`migration_history`, `pg_advisory_lock`), `IMigrationRunnerService`/`MigrationRunnerService` (apply / status / rollback / repair, per-file transaction), and a `DatabaseProvider` + `IMigrationDialect`/`PostgresMigrationDialect` seam that **owns all PG SQL + the advisory-lock/DDL strings**. Host-agnostic — depends only on a connection factory + `Npgsql` + `Dapper` + `ILogger`. (§2 below still sketches the older `IMigrationRunner`/`IMigrationTracker`/`MigrationRunnerOptions` names — that drift is reconciled at the §6 extraction step.)
- **CLI** `smart-qr-migrate` — `status · apply · rollback · verify · new · merge · help`. Dependency-free arg parsing for the POC (System.CommandLine + Spectre deferred to extraction).
- **Wiring**: deleted EF `EnsureCreated`; `001-baseline/{Apply,Rollback}.sql` hand-authored to mirror the EF model; SQL files embedded **and** on disk (runtime reads embedded, CLI reads filesystem).
- **Verified**: build 0/0 · 17 tests green · CLI `apply` builds the 4 tables, `status` + idempotent re-apply, `rollback`→re-apply, `verify` (no-drift), `new` file-gen. The Api on a **fresh DB** auto-migrated from the **embedded** source at startup (6 ms) → `POST /api/codes` returns **200** (previously 500), row persisted with `user_id`. Both sources confirmed.

### Implementation deltas (refine the design below)

| Delta | Detail |
|---|---|
| Reserved word | `routing_rules."order"` must be **double-quoted** in hand SQL (EF auto-quotes; the migrator does not). |
| Dual-host migrate | POC: **both** `SmartQr.Api` + `SmartQr.Redirect` call `MigrateSmartQrDatabaseAsync` at startup — idempotent under the advisory lock; whichever boots first applies. Prod narrows to one host / an init step. |
| Embedded names | `<EmbeddedResource Include="SqlFiles\**\*.sql"><LogicalName>%(RecursiveDir)%(Filename)%(Extension)</LogicalName>` → `Migrations/001-baseline/Apply.sql`; parsed by `EmbeddedResourceMigrationSource`. Confirmed in the built DLL. |
| Runtime split | smart-qr is **.NET 9** (this SDK targets .NET 9 too per repo CLAUDE.md). CLI is a plain `Exe` (`dotnet run`), not yet `PackAsTool` — that lands at extraction. |
| Connection seam | smart-qr ran the engine on its concrete `DbConnectionFactory` (returned `NpgsqlConnection`). **Re-pointed at extraction (2026-06-13)** to the SDK `IDbConnectionFactory` (BCL `DbConnection`). Connection/transaction params are now `System.Data.Common.DbConnection` / `DbTransaction`; **no downcast needed** (all calls are Dapper or BCL `DbConnection`). Only genuinely-Npgsql site is `PostgresMigrationDialect.EnsureDatabaseExistsAsync` (self-constructed `NpgsqlConnection` for the maintenance-DB `CREATE DATABASE`), kept as-is. |
| `migration_history` | `ordinal` is PK (single-column); `char(64)` checksum read back is `.Trim()`-guarded. `applied_by` observed: `cli`, `startup` (`endpoint` reserved for the HTTP host). |
| Naming polish (2026-06-12) | Renamed vs §2 sketch: `IMigrationRunner`→`IMigrationRunnerService`, `MigrationRunner`→`MigrationRunnerService`, `IMigrationTracker`/`MigrationTracker`→`IMigrationHistoryRepository`/`MigrationHistoryRepository`, `AppliedMigration`→`MigrationHistoryEntry`, `MigrationScanner`→`IMigrationScanner`/`MigrationScannerService`, `MigrationRunnerOptions`→`MigrationOptions`, `AddSqlMigrationsRunner`→`AddSqlMigrations`. No `IMigrationDevRunner` split — rollback/repair sit on the one runner interface, hard-gated by `MigrationOptions.AllowRollback`. §6 decides which name-set wins at extraction. |
| Dialect seam | A `DatabaseProvider` enum + `IMigrationDialect`/`PostgresMigrationDialect` (absent from §2's sketch) owns **all** Postgres SQL — advisory lock/unlock, history DDL, the `EnsureDatabaseExistsAsync` create-database step. This is the multi-provider extension point §0.1/§6 want; the SQLite dialect for drydock/secrets-vault drops in here. |
| `applied_by` is a free string | Impl stamps `applied_by` as a plain `string` (`"cli"`, `"startup"`), **not** the proposed `MigrationActor` enum + a `CHECK` constraint (§2.4). Decide at extraction whether to harden to the enum+CHECK or keep the free string. |
| Startup auto-apply ungated | Both hosts call apply unconditionally at boot (no `IsDevelopment()` gate), unlike the §3.2 hosted-service sketch. Fine for the POC; the SDK's hosted-service adapter must add the env gate before shipping. |
| Orphan + repair hardening (2026-06-13) | Apply now **fails closed** on orphaned history (`MigrationOrphanException`, opt-out via `MigrationOptions.AllowOrphanedHistory`); `RepairAsync` is `AllowRollback`-gated and runs under the advisory lock; CLI destructive verbs (`rollback`, `verify --repair`) require `--i-understand-this-is <db>` + `[y/N]` (or `--force`) and the engine no longer hard-wires `AllowRollback=true`. Fold into the §2.5 / §4.4 design when extracting. |

---

## 0. TL;DR

| Aspect | Decision |
|---|---|
| Engine | Bespoke runner over raw `.sql`. No FluentMigrator / Grate / DbUp at runtime. |
| Deps | `IDbConnectionFactory` + `Npgsql` (from the **new BCL-only `…Data.Abstractions` package** — see §0.1 prerequisite), `Dapper`, `Microsoft.Extensions.Logging.Abstractions`, one options record. **Zero** domain types, **zero** ASP.NET/web/MediatR/Hosting deps in the engine source. |
| Hosts | One `IMigrationRunner`, one shared `AddSqlMigrationsRunner(…)` registration, three thin adapters: CLI (build now), HTTP endpoint (later), hosted-service (dev auto-apply only). |
| On disk | `SqlFiles/Migrations/{NNN}-{name}/{Apply.sql,Rollback.sql}` + flat `Dev/*.sql` promoted to numbered pairs when stable. This **IS** the schema-first source-of-truth (§5.1). |
| Sources | `IMigrationSource`: `FileSystemSource` (dev), `EmbeddedResourceSource` (prod), `CompositeSource`. |
| Tracking | `migration_history` — `ordinal INT` PK + UNIQUE, `checksum CHAR(64)` (normalized SHA-256), `applied_by` ∈ {startup, endpoint, cli}. |
| Concurrency | `pg_advisory_lock` serializes the apply loop (primary); `ordinal UNIQUE` is defense-in-depth. **One canonical lock id, shared by all hosts.** |
| Tx model | Per-file transaction (Postgres transactional DDL) + `-- @no-transaction` escape hatch (weaker idempotency — see §2.5). |
| Checksum | SHA-256 over **normalized** `Apply.sql` (LF-only, trimmed) → no false drift. Rollback + directives **excluded** (documented blind spot, §2.6). |
| Rollback | **Dev/test only.** Engine **hard-enforces** via `options.AllowRollback` (not host memory). Prod = roll forward. |
| Squash | `rebaseline` = `pg_dump --schema-only` → `001-baseline/Apply.sql` + mark-as-applied (record checksum, don't run) for live DBs. `pg_dump` is an **external runtime dep**; `information_schema` fallback is first-class (§4.1). |
| EF role | Pure mapper over the SQL-owned schema. Delete `EnsureCreated`; strip migration-only EF config per `database.md` waste-rule (§6 step 7). Testcontainers conformance test asserts EF round-trips every entity. |
| Packaging | **Engine = folder in the mono-lib** (inherits its ASP.NET FrameworkReference; web-freedom enforced by an **arch test**, not a csproj boundary). **CLI = separate web-free `dotnet tool` package OUTSIDE the mono-lib**, referencing `…Data.Abstractions` + a copy of the engine sources, never the mono-lib. See §0.1 + §4.1. |

### 0.1 Hard prerequisite — extract `IDbConnectionFactory` into a BCL-only package

> **This blocks the whole host-agnostic story. Resolve it first.**

`IDbConnectionFactory` / `DataSourceConnectionFactory` currently live in `src/Data/Dapper/` **inside the mono-lib** (`src/WoW.Two.Sdk.Backend.Beta.csproj`), which carries `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` (verified at `src/WoW.Two.Sdk.Backend.Beta.csproj:32`) plus Hangfire.AspNetCore, OAuth, OpenTelemetry.AspNetCore. There is **no standalone `…Data.Dapper` package** — it is a folder. Any reference to obtain `IDbConnectionFactory` therefore drags the entire ASP.NET-bearing mono-lib transitively.

A `dotnet tool` CLI that pulled the mono-lib would ship the whole ASP.NET surface — defeating the point. So:

1. Create a tiny **BCL-only** package/csproj `WoW.Two.Sdk.Backend.Beta.Data.Abstractions` (outside the mono-lib), depending **only** on `System.Data.Common` + `Npgsql`. **No `FrameworkReference`.**
2. Move `IDbConnectionFactory` + `DataSourceConnectionFactory` there (or mirror the interface; the mono-lib re-exports/type-forwards for existing consumers).
3. The migrator engine and the CLI reference **`…Data.Abstractions`**, never the mono-lib.

Until this split exists, the "host-agnostic, zero-web-dep" property is **unenforceable** for the CLI host. The engine extraction (§6) is gated on it.

---

## 1. Why bespoke (vs DbUp / Grate / EF)

This folder already ships DbUp and EF runners. The bespoke engine exists because none of them satisfy the full constraint set at once:

| Need | EF migrations | DbUp | Grate | **Bespoke** |
|---|---|---|---|---|
| Schema owned by **raw SQL**, EF a pure mapper | ✗ (C# owns schema) | ✓ | ✓ | ✓ |
| **Host-agnostic** core reusable by CLI + HTTP + hosted-service | ✗ (DbContext-coupled) | partial (hosted-service shaped) | ✗ (CLI-shaped) | ✓ |
| **Normalized** checksum (no CRLF/whitespace false drift) | n/a | hash on raw bytes | hash on raw bytes | ✓ |
| Per-file **transactional DDL** + per-file `@no-transaction` opt-out | coarse | limited | limited | ✓ |
| **Advisory-lock** multi-instance apply | ✗ | ✗ | partial | ✓ |
| **Rebaseline/squash** via `pg_dump` + mark-as-applied | ✗ | ✗ | ✗ | ✓ |
| `Dev/` flat-file promotion to dodge ordinal branch-collision | ✗ | ✗ | ✗ | ✓ |
| Sources: filesystem (dev, no rebuild) **and** embedded (prod) | ✗ | embedded-only | filesystem-only | ✓ (`CompositeSource`) |

**Verdict:** the engine is small (a few hundred lines), owns exactly the semantics we want, and reuses what smart-qr already has at the *driver* level (`NpgsqlDataSource`, Dapper, snake_case via `EFCore.NamingConventions` + Dapper underscore mapping). The cost of bespoke is below the cost of bending a third-party tool to host-agnostic + normalized-checksum + rebaseline. `Ef/` stays for code-first consumers; `DbUp/` stays as a legacy option; **`Sql/` is the intended default for wow-two products.**

> **Honest seam note:** smart-qr does **not** yet have the SDK's `IDbConnectionFactory` abstraction — it has a *concrete* `SmartQr.Common.Persistence.DbConnectionFactory` (no interface) returning `NpgsqlConnection`, built from `NpgsqlDataSource` (verified at `SmartQr.Common/Persistence/DbConnectionFactory.cs`). The engine is proven against that concrete factory, then **re-pointed** to the SDK `IDbConnectionFactory : DbConnection` shape on extraction. That re-point is a real (small) API change, not a no-op — see §6 step 5.

---

## 2. Engine architecture

### 2.1 Host-agnostic core

The engine source depends only on:

- `IDbConnectionFactory` — from the new **`WoW.Two.Sdk.Backend.Beta.Data.Abstractions`** package (§0.1), BCL surface (`DbConnection` / `DbDataSource`). **Not** the mono-lib.
- `Npgsql` — provider (advisory lock, transactional DDL). Standalone package ref (§2.1.1).
- `Dapper` — thin mapping for `migration_history` reads/writes.
- `Microsoft.Extensions.Logging.Abstractions` — `ILogger`.
- `Microsoft.Extensions.DependencyInjection.Abstractions` — for the one `AddSqlMigrationsRunner` extension only.
- `MigrationRunnerOptions` — one record.

It references **no** domain entities, **no** MediatR, **no** ASP.NET, **no** `Microsoft.Extensions.Hosting`, **no** CLI framework. The hosted-service adapter's `IHostedService`/`IHostEnvironment` dependency lives in the **host-adapter project, not the engine**, so the engine stays Hosting-free. This is what lets the hosts share one engine.

> Because the engine ships as a **folder in the mono-lib** (§0/§4.1), the *csproj* still carries ASP.NET transitively. Web-freedom is therefore a property of the **source** (the imports), enforced by an architecture test + namespace discipline (§6 step 4), not by a project boundary. The CLI's copy of these sources compiles in a genuinely web-free csproj where the property is also enforced by a transitive-package assertion.

#### 2.1.1 Provider leakage is explicit, not hidden

The API is typed on BCL `DbConnection` (the SDK abstraction is BCL-only), but the **semantics are Postgres-only**: `pg_advisory_lock`, transactional DDL, `pg_dump`. Advisory lock and `BeginTransaction` are issued as **raw SQL over the `DbConnection`** (no provider downcast needed for lock/unlock — they're plain `SELECT`s); where a genuinely Npgsql-typed member is required, the engine downcasts `DbConnection` → `NpgsqlConnection` at a **single documented cast point** in `PostgresMigrationTracker`. The abstraction is BCL in signature, Postgres in fact, and the doc says so rather than pretending portability.

### 2.2 Public API sketch

```csharp
namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>The host-facing entry point. Every host (CLI, HTTP, hosted-service) resolves this.
/// Apply/status only — no destructive ops.</summary>
public interface IMigrationRunner
{
    Task<MigrationApplyResult>  ApplyPendingAsync(CancellationToken ct = default);
    Task<MigrationStatusResult> GetStatusAsync(CancellationToken ct = default);
}

/// <summary>Dev/test-only extension of the runner — destructive ops. Registered ONLY when
/// options.AllowRollback is true (dev/CLI). Each method ALSO hard-checks AllowRollback and
/// throws when false, so the guard lives in the engine, not each host's memory.</summary>
public interface IMigrationDevRunner : IMigrationRunner
{
    Task<MigrationRollbackResult> RollbackAsync(int count = 1, CancellationToken ct = default);
    Task<BaselineResult>          BaselineAsync(BaselineRequest request, CancellationToken ct = default);
}

/// <summary>Reads migration descriptors from a backing store (disk / embedded / composite).</summary>
public interface IMigrationSource
{
    IReadOnlyList<MigrationDescriptor> Scan();      // ordered by ordinal, ascending
}

/// <summary>Owns the migration_history table — idempotent create, read applied, record one.</summary>
public interface IMigrationTracker
{
    Task EnsureTableAsync(DbConnection conn, CancellationToken ct);
    Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(DbConnection conn, CancellationToken ct);
    Task RecordAsync(DbConnection conn, DbTransaction? tx, AppliedMigration row, CancellationToken ct);
    Task RemoveAsync(DbConnection conn, int ordinal, CancellationToken ct);   // rollback only
}

public sealed record MigrationDescriptor(
    int Ordinal, string Version, string Name,
    string ApplySql, string? RollbackSql,
    string Checksum,            // normalized SHA-256 of ApplySql, precomputed on scan
    bool NoTransaction);        // parsed from `-- @no-transaction`

public sealed record AppliedMigration(
    int Ordinal, string Version, string Name, string Checksum,
    DateTimeOffset AppliedAt, string AppliedBy, int ExecutionMs);

public enum MigrationActor { Startup, Endpoint, Cli }   // -> migration_history.applied_by

public sealed record MigrationRunnerOptions
{
    public string  Schema              { get; init; } = "public";
    public string  HistoryTable        { get; init; } = "migration_history";

    /// <summary>CANONICAL advisory-lock id. Do NOT override per-host — all hosts of one DB
    /// MUST share this value or they will not serialize against each other. Constant by default.</summary>
    public long    AdvisoryLockId      { get; init; } = CanonicalAdvisoryLockId;   // 0x_77_6F_77_32_5F_6D ("wow2_m")

    public string  VersionLabel        { get; init; } = "0";   // free label (e.g. semver tag); ordinal is the gate
    public MigrationActor AppliedBy     { get; init; } = MigrationActor.Cli;
    public bool    AllowRollback        { get; init; }          // false in prod hosts; gates IMigrationDevRunner
    public bool    FailOnChecksumDrift  { get; init; } = true;

    public const long CanonicalAdvisoryLockId = 0x_77_6F_77_32_5F_6D;
}
```

`MigrationApplyResult` / `MigrationStatusResult` / `MigrationRollbackResult` carry per-file rows (ordinal, name, outcome ∈ applied|skipped|drift|failed|orphaned, `execution_ms`) so every host renders the same data its own way. A `MigrationDriftException` (carrying the drifted ordinals) is the single failure type so a host can map it to a distinct exit code (§4.4).

### 2.3 `IMigrationSource` implementations

| Source | Use | Behavior |
|---|---|---|
| `FileSystemSource(rootPath)` | Dev — editable, **no rebuild** | Reads `SqlFiles/Migrations/{NNN}-{name}/{Apply,Rollback}.sql` off disk. |
| `EmbeddedResourceSource(assembly, prefix)` | Prod — self-contained | Reads the same pairs embedded as resources; ships inside the app. |
| `CompositeSource(params IMigrationSource[])` | Mixed | Merges by ordinal; first non-empty wins per ordinal. Lets prod ship embedded while dev overlays filesystem. |

All three return `MigrationDescriptor` with the checksum already normalized + computed, so the runner is source-agnostic.

### 2.4 Tracking table DDL

```sql
CREATE TABLE IF NOT EXISTS migration_history (
    ordinal      INTEGER     NOT NULL,
    version      TEXT        NOT NULL,
    name         TEXT        NOT NULL,
    checksum     CHAR(64)    NOT NULL,             -- normalized SHA-256 of Apply.sql, lowercase hex
    applied_at   TIMESTAMPTZ NOT NULL,             -- set by code in RecordAsync (see note), not DB default
    applied_by   TEXT        NOT NULL,             -- 'startup' | 'endpoint' | 'cli'
    execution_ms INTEGER     NOT NULL,
    CONSTRAINT pk_migration_history PRIMARY KEY (ordinal),     -- PK already implies UNIQUE; the gate
    CONSTRAINT ck_migration_history_applied_by
        CHECK (applied_by IN ('startup', 'endpoint', 'cli'))
);
```

`ordinal` PK is the **defense-in-depth** concurrency gate (the advisory lock is primary — §2.5): if the lock is ever bypassed, the loser of a duplicate insert hits the PK violation, rolls back its file, and re-reads state. The redundant `uq_…_ordinal` UNIQUE from the prior draft is dropped — a PK is already unique. `applied_by` CHECK keeps the actor enum honest.

> **`applied_at` — code-set, not `DEFAULT now()`.** `execution_ms`, `applied_by`, and `checksum` are all computed in code per row, and the §2.5 loop already times the apply in code (`sw = Stopwatch`). Per `database.md`, `DEFAULT NOW()` is for timestamps **except when the value must be provided by code** — this is that case. `RecordAsync` sets `applied_at` from the same clock, so there is no never-fires DB default. (A bookkeeping-table `DEFAULT now()` would be defensible, but code-set is consistent with the other code-owned columns.)

### 2.5 Concurrency, transaction, and no-transaction semantics

**One primary concurrency model: the advisory lock serializes the apply loop.** The `ordinal` PK is secondary (defense-in-depth), not "the gate."

Apply loop (pseudocode — see `MigrationRunner`):

```
conn = await factory.CreateOpenAsync()
await SELECT pg_advisory_lock(:CanonicalAdvisoryLockId)   -- serialize ALL hosts of this DB (CLI, startup, endpoint)
try:
    await tracker.EnsureTableAsync(conn)
    applied = await tracker.GetAppliedAsync(conn)          -- ordinals + checksums

    -- (1) DRIFT CHECK on ALREADY-APPLIED files, before applying anything:
    foreach d in source.Scan() where d.Ordinal in applied.ordinals:
        if d.Checksum != applied[d.Ordinal].checksum:
            record drift(d)
            if options.FailOnChecksumDrift: throw MigrationDriftException(driftedOrdinals)
            else: log.Warn(...)                            -- never re-runs an applied file

    -- (2) APPLY pending:
    pending = source.Scan() where d.Ordinal not in applied.ordinals
    foreach d in pending (ascending ordinal):
        sw = Stopwatch.start
        if d.NoTransaction:
            await Execute(conn, null, d.ApplySql)          -- e.g. CREATE INDEX CONCURRENTLY
            await tracker.RecordAsync(conn, null, row(d))  -- standalone stmt, no surrounding tx
        else:
            tx = await conn.BeginTransactionAsync()
            await Execute(conn, tx, d.ApplySql)
            await tracker.RecordAsync(conn, tx, row(d))    -- insert; PK protects vs lock bypass
            await tx.CommitAsync()
finally:
    await SELECT pg_advisory_unlock(:CanonicalAdvisoryLockId)
    await conn.DisposeAsync()
```

| Rule | Detail |
|---|---|
| **Advisory lock (primary)** | `pg_advisory_lock(CanonicalAdvisoryLockId)` around the **whole** loop → all hosts of one DB serialize. **Session-scoped:** released in `finally` AND auto-released by Postgres if the connection/process dies (the backend's session ends), so a killed process does **not** leak the lock — the OS-level TCP close ends the session. (`pg_advisory_xact_lock` was considered; it auto-releases at tx end, but the loop spans multiple per-file txs, so a session lock is the right scope.) |
| **Ordinal PK (secondary)** | Only reachable if the lock is bypassed/misconfigured; the duplicate insert fails, the file's tx rolls back, state is re-read. Not the primary mechanism. |
| **Transactional DDL** | Postgres wraps DDL in the per-file tx; a failed file leaves zero partial schema. |
| **`-- @no-transaction`** | First-line directive in `Apply.sql`. Runner runs the file **outside** a tx + records in a standalone statement. For `CREATE INDEX CONCURRENTLY`, `VACUUM`, etc. that forbid running inside a tx block. |
| **Crash — transactional file** | Before commit → not recorded → retried next run (idempotent, clean). After commit → recorded → skipped. |
| **Crash — `@no-transaction` file** | **Weaker guarantee.** A crash between DDL and the standalone `RecordAsync` leaves the schema partially applied **and unrecorded**, so the next run **re-executes** it. Authors of `@no-transaction` files **MUST write idempotent DDL** — `CREATE INDEX CONCURRENTLY IF NOT EXISTS`, guarded `DO $$ … $$` blocks — because crash recovery may re-run an unrecorded, partially-applied file. The `new --no-transaction` template embeds this reminder. |
| **Drift on already-applied** | Checked in step (1) of the loop (not just by `status`). disk checksum ≠ recorded → `FailOnChecksumDrift ? throw MigrationDriftException : warn`. Never silently re-runs. Remediation: `repair` (§4.4). |

### 2.6 Normalized checksum

Drift detection compares disk vs `migration_history.checksum`. Both are SHA-256 over **normalized** content so cosmetic edits don't trip false drift:

```csharp
static string Normalize(string raw) => raw
    .Replace("\r\n", "\n").Replace("\r", "\n")   // CRLF/CR -> LF
    .TrimEnd();                                   // trailing whitespace/newlines off
// checksum = SHA256(Normalize(applySql)) -> lowercase hex, 64 chars
```

Computed: (a) on **scan** for every `Apply.sql`; (b) on **apply** before `RecordAsync`; (c) on **rebaseline** over the dumped schema, **without executing**. Editing whitespace/line-endings → same checksum → no drift. Editing actual SQL → drift → caught.

> **Documented blind spot.** Only **`Apply.sql`** is hashed. `Rollback.sql` and the `-- @no-transaction` flag are **excluded** from the checksum — editing a rollback script or flipping the directive on an applied migration is **invisible** to drift detection. Rationale: rollback is a dev/test affordance never run in prod, and a flipped directive only matters at apply time (already passed). Accepted; revisit if it bites.

### 2.7 Rebaseline / squash

`rebaseline` collapses N applied migrations into a fresh `001-baseline`:

1. `pg_dump --schema-only` of a migrated DB → new `Migrations/001-baseline/Apply.sql`. **`pg_dump` is an external runtime dependency** (§4.1) — when absent, fall back to `information_schema` introspection (**first-class**, not parked, since the tool can't bundle `pg_dump`), with a warning that introspection is lower-fidelity (misses some constraints/defaults).
2. Archive old `Migrations/NNN-*` folders.
3. **Mark-as-applied for already-live DBs:** insert the `001-baseline` row into `migration_history` with the dumped-schema checksum **without running** the SQL (schema already exists). Fresh DBs run it normally.

Rollback is a **dev/test affordance only.** Prod rolls forward; `RollbackAsync`/`BaselineAsync` live on `IMigrationDevRunner`, are registered only when `AllowRollback` is true, and **also** hard-check `AllowRollback` and throw when false. Nothing destructive is ever wired into the HTTP or hosted-service hosts.

---

## 3. One engine, three hosts

```
                    ┌────────────────────────────────────────┐
                    │  IMigrationRunner  (the engine source)  │
                    │  Npgsql · Dapper · ILogger ·            │
                    │  IDbConnectionFactory (…Data.Abstractions) │
                    │  ZERO web / domain / CLI / Hosting deps  │
                    │  wired ONCE by AddSqlMigrationsRunner    │
                    └───────────────────┬─────────────────────┘
            ┌───────────────────────────┼───────────────────────────┐
            │                           │                           │
   ┌────────▼─────────┐      ┌──────────▼──────────┐      ┌──────────▼──────────┐
   │  CLI host         │      │  HTTP endpoint       │      │  Hosted service      │
   │  (build NOW)      │      │  (LATER)             │      │  (DEV ONLY)          │
   │  wow-migrate      │      │  POST /_migrations   │      │  auto-apply on boot  │
   │  AppliedBy=Cli    │      │  AppliedBy=Endpoint  │      │  AppliedBy=Startup   │
   │  AllowRollback=   │      │  AllowRollback=false │      │  AllowRollback=false │
   │    env-gated      │      │  (roll forward)      │      │  IsDevelopment() gate│
   └───────────────────┘      └──────────────────────┘      └──────────────────────┘
       (web-free pkg,            (mono-lib folder,             (mono-lib folder,
        separate csproj)          minimal-API adapter)          IHostedService adapter)
```

### 3.1 One canonical registration — wired once, not three times

The engine package ships **one** `AddSqlMigrationsRunner(this IServiceCollection, Action<MigrationRunnerOptions>)` (name parallels `AddDbUpRunner` / `AddEfMigrationsRunner` — descriptive, no `WowTwo` prefix per `naming.md`). It wires the whole graph — runner + tracker + source + options — in one place. Every host calls it; no host re-wires the graph. When `AllowRollback` is true it also registers `IMigrationDevRunner`.

```csharp
// SqlServiceCollectionExtensions.cs (engine package)
public static IServiceCollection AddSqlMigrationsRunner(
    this IServiceCollection services, Action<MigrationRunnerOptions> configure)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configure);

    services.AddOptions<MigrationRunnerOptions>().Configure(configure).ValidateOnStart();
    services.TryAddSingleton<IMigrationTracker, PostgresMigrationTracker>();
    services.TryAddSingleton<IMigrationSource>(/* Composite(FileSystem, Embedded) per options */);
    services.TryAddSingleton<MigrationRunner>();
    services.TryAddSingleton<IMigrationRunner>(sp => sp.GetRequiredService<MigrationRunner>());
    // Dev-only destructive surface — registered only when explicitly allowed:
    services.AddSingleton<IMigrationDevRunner>(sp => sp.GetRequiredService<MigrationRunner>());  // resolvable only if AllowRollback
    return services;
}
```

Per-host overrides (the ONLY differences between hosts):

| Host | `AppliedBy` | `AllowRollback` | Composition root |
|---|---|---|---|
| CLI (`wow-migrate`) | `Cli` | env-gated (false unless dev + target check passes, §4.4) | own minimal `ServiceCollection` → `AddSqlMigrationsRunner` |
| HTTP endpoint | `Endpoint` | **false** (hard) | minimal-API DI → `AddSqlMigrationsRunner` |
| Hosted service | `Startup` | **false** (hard) | Generic-Host DI → `AddSqlMigrationsRunner` |

Nothing lets a prod HTTP/hosted host hold a rollback reference: `IMigrationDevRunner` isn't resolvable when `AllowRollback` is false, and its methods throw on the same flag.

### 3.2 Thin adapters

Each adapter is ~10 lines — resolve `IMigrationRunner`, call one method, render. The hosted-service adapter's `IHostedService`/`IHostEnvironment` dependency lands in the **host-adapter project**, keeping the engine Hosting-free.

```csharp
// CLI adapter (System.CommandLine 2.0 handler) — apply
static async Task<int> Apply(IMigrationRunner runner, CancellationToken ct)
{
    try
    {
        var r = await runner.ApplyPendingAsync(ct);
        foreach (var m in r.Rows) AnsiConsole.MarkupLine($"[{Glyph(m)}] {m.Ordinal:000}-{m.Name} ({m.ExecutionMs} ms)");
        return r.AnyFailed ? 2 : 0;
    }
    catch (MigrationDriftException) { return 1; }   // drift = validation error, exit 1 (matches §4.4 legend)
}

// HTTP adapter (minimal API) — apply, prod-safe (no rollback route registered; AllowRollback hard-false)
app.MapPost("/_migrations/apply", async (IMigrationRunner runner, CancellationToken ct) =>
    Results.Ok(await runner.ApplyPendingAsync(ct)));

// Hosted-service adapter — dev auto-apply only (lives in host project, NOT the engine)
public sealed class MigrationBootstrapService(
    IMigrationRunner runner, IHostEnvironment env, ILogger<MigrationBootstrapService> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment()) { log.LogInformation("Auto-apply skipped (env={Env}).", env.EnvironmentName); return; }
        var r = await runner.ApplyPendingAsync(ct);
        if (r.AnyFailed) throw new InvalidOperationException("Startup migration failed.");
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

## 4. CLI tool — `wow-migrate`

### 4.1 Packaging

The CLI is the one host that **cannot** reference the mono-lib (it would drag ASP.NET into a developer tool). It is a **separate, genuinely web-free package outside the mono-lib**.

- **Local `dotnet tool`** via `.config/dotnet-tools.json` manifest (mirrors `dotnet ef`): version-pinned per repo, `dotnet tool restore` on clone, no global pollution, CI-friendly.
- References **`WoW.Two.Sdk.Backend.Beta.Data.Abstractions`** (§0.1) for `IDbConnectionFactory`, plus a **shared copy of the engine sources** (linked `<Compile Include="../Sql/**/*.cs">` or a thin web-free engine csproj that also lives outside the mono-lib). It **never** references `WoW.Two.Sdk.Backend.Beta.csproj`.
- **No** `<FrameworkReference Include="Microsoft.AspNetCore.App"/>`. A CI assertion (`dotnet list <cli>.csproj package --include-transitive`) fails the build if `Microsoft.AspNetCore.*` / `MediatR` appears (§6 step 4).

**Cross-platform realities (.NET 10):**

1. **`pg_dump` is an external runtime dependency** of `rebaseline` — not bundled. Its client version must be **≥ the server's**, or the dump fails or silently omits objects. Pin a **minimum client version** in docs; make the `information_schema` fallback (§2.7) a first-class path since the tool can't ship `pg_dump`.
2. **.NET 10 RID-specific tool packages.** On .NET 10, `dotnet pack` with **any** `RuntimeIdentifiers` present now produces RID-specific tool packages instead of the framework-dependent, platform-agnostic one (documented breaking change). To keep one cross-platform `dotnet-tools.json` entry working everywhere, set `<CreateRidSpecificToolPackages>false</CreateRidSpecificToolPackages>` and avoid `RuntimeIdentifiers` in the CLI csproj. (Use `ToolPackageRuntimeIdentifiers` only if multi-RID is ever wanted.)

```jsonc
// .config/dotnet-tools.json
{ "version": 1, "isRoot": true,
  "tools": { "WoW.Two.Sdk.Backend.Beta.Migrations.Cli": { "version": "0.0.*", "commands": ["wow-migrate"] } } }
```

```xml
<!-- WoW.Two.Sdk.Backend.Beta.Migrations.Cli.csproj  (OUTSIDE the mono-lib; web-free) -->
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>wow-migrate</ToolCommandName>
  <PackageId>WoW.Two.Sdk.Backend.Beta.Migrations.Cli</PackageId>   <!-- dotted WoW.Two, matches ecosystem -->
  <CreateRidSpecificToolPackages>false</CreateRidSpecificToolPackages> <!-- .NET 10: keep platform-agnostic -->
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="../../Abstractions/WoW.Two.Sdk.Backend.Beta.Data.Abstractions.csproj" />
  <!-- engine sources shared via linked compile or a web-free engine csproj — NOT the mono-lib -->
  <PackageReference Include="System.CommandLine" />
  <PackageReference Include="Spectre.Console" />   <!-- status tables / apply progress -->
  <PackageReference Include="Npgsql" />
</ItemGroup>
```

> **CPM (Central Package Management) is ON** in this repo (`src/Directory.Packages.props`, `ManagePackageVersionsCentrally=true`). Versionless `<PackageReference>` only works if a central `<PackageVersion>` exists. **Add to `Directory.Packages.props`:** `System.CommandLine`, `Spectre.Console`, and a **standalone `Npgsql`** (none exist today — only `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0` is pinned; pulling Npgsql transitively via the EF package would re-couple the web-free engine to the EF provider). Align the standalone `Npgsql` version to what the EF provider `10.0.0` resolves. See §4.1.1.

#### 4.1.1 `Directory.Packages.props` additions (required, CPM)

```xml
<PackageVersion Include="System.CommandLine" Version="2.0.x" />  <!-- pin exact 2.0 line; NOT a beta -->
<PackageVersion Include="Spectre.Console"   Version="0.49.x" />
<PackageVersion Include="Npgsql"            Version="10.0.x" />  <!-- standalone; align to EF provider 10.0.0 -->
```

### 4.2 Library choice — `System.CommandLine` 2.0

Microsoft-owned, DI-friendly, native sub-commands, minimal deps. **Not "ships with the SDK"** — the copy inside the `dotnet` muxer is internal/un-referenceable; consumers must add the NuGet package explicitly (which §4.1 does). **Pin an exact 2.0 line** in `Directory.Packages.props`: the package churned through `2.0.0-beta*` (breaking changes across beta5/beta7) before the non-prerelease 2.0 line landed in 2026. Write the §3/§4 handler signatures against the **final 2.0 API**, not a beta tutorial. `Spectre.Console` is rendering-only (`status` table + apply glyphs). Rejected: Cocona / McMaster (extra dep, weaker DI), Spectre.Console.Cli (no built-in DI).

### 4.3 Config / file discovery — resolution order

Highest precedence wins. Fail fast with the full searched-list on miss. **The resolved connection string is NEVER echoed** — password is redacted in all logs and error output, including the fail-fast list.

| # | Source | Sets | Secret handling |
|---|---|---|---|
| 1 | CLI flags: `--sql-dir`, `--connection` | dir + conn (one-offs) | Plaintext `--connection` lands in shell history / `ps`. Prefer `--connection-env NAME` (read conn from env var `NAME`) or `--connection-stdin`. |
| 2 | `WOW_MIGRATE_SQL_DIR`, `WOW_MIGRATE_CONNECTION` env vars | dir + conn | Preferred for secrets. |
| 3 | `migrations.json` (repo root or nearest parent) — `${ENV}` interpolation | dir + conn template | Commit the template only; an **unset `${VAR}` is a hard fail**, never a silent empty password. |
| 4 | `appsettings.Development.json` → `ConnectionStrings:DefaultConnection` | conn **template only** | This file is routinely committed — treat it as a **non-secret template**. Real secrets must come from env / `*.local.json`. |
| 5 | CWD convention — scan cwd + ≤5 parents for `.csproj`, infer `<repo>/SqlFiles/Migrations` | dir | Default for the **canonical product layout only**; overridable. Not a universal hardcoded path. |

```jsonc
// migrations.json  (commit the template; gitignore any *.local.json with real secrets)
{
  "provider": "postgres",
  "sqlDir": "./SqlFiles/Migrations",
  "connection": "Host=localhost;Port=5432;Database=smart_qr_dev;Username=postgres;Password=${WOW_MIGRATE_PASSWORD}",
  "schema": "public"
  // NOTE: advisoryLockId is intentionally NOT settable here — it is a canonical engine constant
  // shared by all hosts of one DB (§2.5). Overriding it per-product would break multi-host serialization.
}
```

> **`dotnet tool restore` discovery:** the manifest is found by walking up from the **cwd**. Running `wow-migrate` from a sibling directory outside the manifest's tree silently fails to restore. Invoke from the repo subtree, or pass `--sql-dir` explicitly. When the convention scan (#5) is ambiguous (multi-project repo, manifest root ≠ SqlFiles location), `--sql-dir` is **required** rather than guessed.

### 4.4 Command table

| Command | Args / flags | Prints | Dev-only? | Engine call |
|---|---|---|---|---|
| `init` | `--sql-dir`, `--connection` | scaffolded tree + `migrations.json` template + next-steps | no | — (filesystem scaffold) |
| `new <name>` | `--description`, `--no-transaction` | created **flat** `Dev/<utc-ts>_<name>.sql` (branch-safe; see §4.6) | no | — (writes dev template) |
| `apply` | `--dry-run`, `--up-to <NNN>` | per-file `[✓ applied / ⊘ skipped / ⚠ drift / ✗ failed] NNN-name (ms)` + summary | no | `ApplyPendingAsync` |
| `status` | — | table: ordinal · name · applied/pending/drift/**orphaned** · checksum · applied_at · applied_by + `Dev/` in-flight list | no | `GetStatusAsync` |
| `rollback` | `--count <N>`, `--force` | per-file rolled-back rows; **target-gated** (§ guard) | **yes** | `RollbackAsync(count)` |
| `rebaseline` | `--backup-file`, `--output-sql`, `--force` | dumped `001-baseline/Apply.sql` path + history marks | **yes** | `BaselineAsync` |
| `promote` | `[--all]`, `--dev-file <f>`, `--keep-dev`, `--check-only` | promoted `Dev/x.sql → NNN-x/{Apply,Rollback}.sql`; warns on stub Rollback | no | — (allocates ordinal, splits + writes) |
| `repair` | `--ordinal <NNN>`, `--force` | rewrites recorded `migration_history.checksum` to the current disk checksum (accept an intentional edit) | **yes** | dev-only tracker write |
| `verify` | `--up-to <NNN>`, `--image <postgres:tag>` | Testcontainers apply→rollback→re-apply→checksum-match report | **yes** | `ApplyPendingAsync` + `RollbackAsync` (throwaway container) |

`new` emits a **flat `Dev/` file** (not a numbered folder) — there is **no** direct-to-`Migrations/NNN-` authoring path, so two branches never compute the same ordinal (§4.6). `promote` is the only thing that allocates an ordinal, and only at merge time.

**Exit codes:**

| Code | Meaning | Sources |
|---|---|---|
| `0` | success | normal apply / status |
| `1` | **validation error** | bad config, missing file, **checksum drift with fail-on** (`MigrationDriftException`) |
| `2` | **execution error** | DB error, destructive-op guard tripped, verify failure |

The §3.2 CLI `apply` adapter maps `MigrationDriftException` → exit **1** (not blanket 2 via `AnyFailed`), matching this legend.

**`orphaned`** = a `migration_history` row whose source file is absent (e.g. after a `rebaseline` archive, or a deleted file). `status` reports it. **Prod (roll-forward) reaction: warn only** — roll-forward never deletes history, so an orphan is informational, not a failure.

**Destructive-op guard — gates on the TARGET DB, not `ASPNETCORE_ENVIRONMENT`:** as a standalone local tool, `wow-migrate` runs under whatever the developer's shell has (often unset → treated as non-prod) while `--connection` may point straight at production. So `rollback` / `rebaseline` / `verify` / `repair` refuse unless the operator confirms the target: require `--i-understand-this-is <dbname>` to match the connection's database (or detect a prod marker row in the DB). The legacy `ASPNETCORE_ENVIRONMENT=Production` / `--prod` check is kept only as an **additional** block, never the sole axis. Destructive ops also prompt `[y/N]` unless `--force`. (There is **no** `truncate` command — the prior "truncate" guard prose was a dangling reference and is removed.)

### 4.5 File-gen templates

`new <name>` writes a flat `Dev/` file; `promote` splits it into the numbered pair. Header is parseable metadata; `-- @no-transaction` is the runtime directive:

```sql
-- Apply.sql  (Dev/ file carries both sections; promote splits them)
-- @version 003
-- @name add-users-table
-- @description Create users table + unique email index
-- @applied_by cli
-- @created_at 2026-06-11T10:30:00Z
-- @no-transaction        ← include ONLY for CONCURRENTLY / VACUUM etc.
--                          REMINDER: @no-transaction files MUST be idempotent
--                          (CREATE INDEX CONCURRENTLY IF NOT EXISTS, guarded DO blocks) — see §2.5

CREATE TABLE users (
    id    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email TEXT NOT NULL UNIQUE
);
```

```sql
-- Rollback.sql   (dev/test affordance — inverse of Apply; NOT part of the checksum, §2.6)
-- @version 003

DROP TABLE IF EXISTS users;
```

### 4.6 Ordinal allocation + Dev promotion (single authoring path)

There is **one** authoring path: all in-flight work starts as a flat `Dev/*.sql`; `promote` is the only thing that allocates an ordinal. This is deliberate — a direct `new → Migrations/NNN-` path would let two branches compute the same `NNN` (the exact collision `Dev/` exists to dodge).

- **In-flight:** `new` lands work as flat `Dev/<utc-ts>_<slug>.sql` (no ordinal → no merge collision). Two branches each adding a `Dev/*.sql` merge cleanly.
- **Allocation — at merge time only:** ordinals are assigned **on the integration branch**, not on feature branches. `promote` scans `Migrations/^\d{3}-` → `next = max(ordinal) + 1` (or `001`). Because allocation happens only post-merge on one branch, two feature branches never both pick `NNN`.
- **Concurrent `promote` on the integration branch:** if two promotes race and an ordinal is taken, the second **re-allocates to the next free slot**. To make "free" unambiguous, promotion is **CI-serialized on the integration branch** (a single promote job, not parallel); locally, `promote --check-only` reports the collision and a human resolves the final `NNN`. Filesystem `max()+1` is **not** asserted collision-safe across parallel actors — serialization is the arbiter.

```
authoring:  new  →  SqlFiles/Migrations/Dev/<utc-ts>_<slug>.sql   (flat, editable, no rebuild, branch-safe)
allocation: promote (integration branch, CI-serialized)  →  next = max(Migrations/^\d{3}-) + 1
result:     Dev/<f>.sql  →  Migrations/<NNN>-<name>/{Apply,Rollback}.sql
```

---

## 5. On-disk layout

```
src/Data/Migrations/BespokeFiles/
└── Migrations/
    ├── 001-baseline/
    │   ├── Apply.sql
    │   └── Rollback.sql
    ├── 002-add-users-table/
    │   ├── Apply.sql
    │   └── Rollback.sql
    ├── 003-add-email-index/
    │   ├── Apply.sql            -- @no-transaction (CREATE INDEX CONCURRENTLY IF NOT EXISTS)
    │   └── Rollback.sql
    └── Dev/                     ← flat in-flight files, branch-safe, promoted when stable
        ├── .gitkeep
        └── 20260611T1030_add-service-column.sql
```

**Engine code** ships as a **folder in the mono-lib** (no own csproj — globbed into `src/WoW.Two.Sdk.Backend.Beta.csproj`, exactly like `Ef/` and `DbUp/`):

```
src/Data/Migrations/Bespoke/
├── IMigrationRunner.cs · IMigrationDevRunner.cs · MigrationRunner.cs
├── IMigrationSource.cs · FileSystemSource.cs · EmbeddedResourceSource.cs · CompositeSource.cs
├── IMigrationTracker.cs · PostgresMigrationTracker.cs        ← single DbConnection→NpgsqlConnection cast point
├── MigrationDescriptor.cs · AppliedMigration.cs · *Result.cs · MigrationDriftException.cs · MigrationRunnerOptions.cs
├── MigrationContentNormalizer.cs · OrdinalAllocator.cs
├── SqlServiceCollectionExtensions.cs                          ← AddSqlMigrationsRunner (parallels AddDbUpRunner)
├── MigrationBootstrapService.cs                               ← IHostedService adapter (mirrors DbUpHostedService)*
├── sql.md                                                     ← this doc (folder lead doc)
├── SqlMigrations.standard.md  (RFC 2119 contract — when API firms)
└── SqlMigrations.spec.md      (API + snippets — when API firms)
```

\* `MigrationBootstrapService` depends on `Microsoft.Extensions.Hosting.Abstractions`. In the mono-lib that's fine (the framework reference supplies it); for the **web-free CLI/engine package** it is **excluded** (the CLI has no hosted-service). The engine *runner* itself stays Hosting-free; only this adapter file touches Hosting.

The CLI lives **outside** the mono-lib in a sibling web-free package (`Migrations/Cli/` as a standalone csproj, §4.1), sharing the engine sources via linked compile — never referencing the mono-lib.

> **Naming:** sibling contract/spec docs follow the module name (PascalCase) like `Mediator.standard.md` / `Testing.standard.md` on disk. The module here is **SqlMigrations** (the runner family) — `Sql.*` would collide with the sibling `Data/Dapper` SQL surface — so the files are `SqlMigrations.standard.md` / `SqlMigrations.spec.md`. The lead doc (`sql.md`) H1 keeps the full package name; the standard/spec carry the module name.

### 5.1 Schema-first source-of-truth (`database.md` anchor)

`conventions/development/backend/persistence/database.md` mandates reading the canonical schema (`migrations/*.sql` for SDK consumers) **before** writing any models / queries / EF configs. For wow-two products using this runner, **`SqlFiles/Migrations/*/Apply.sql` IS that canonical schema** — the owned `CREATE TABLE` truth that the schema-first rule requires reading first, and that EF maps over (§6 step 7). This supersedes the bare `migrations/` path named in `database.md`; update that pointer to `SqlFiles/Migrations/*/Apply.sql` when `Sql/` ships.

---

## 6. Build-then-extract plan (smart-qr → SDK)

> **Not a file move.** The engine was **built greenfield in `smart-qr` first**, proven against a real product, **then** copied here (smart-qr untouched — it consumes the package post-publish). Extraction was a copy + namespace rewrite **plus** a real connection-factory re-point (step 5) — so it is *not* "no logic change." **Steps 1–3, 5 and the status half of 9 are DONE (2026-06-13).** Remaining: the `…Data.Abstractions` split (0), the arch/transitive ref tests (4), CLI packaging (6), EF→mapper strip (7), Testcontainers conformance (8), and the `../migrations.md` index (9).

**Gating prerequisite (do first):** §0.1 — extract `IDbConnectionFactory` + `DataSourceConnectionFactory` into the BCL-only `…Data.Abstractions` package. The engine cannot be web-free for the CLI host until this exists.

| Step | Action |
|---|---|
| 0 | **Prereq:** ship `…Data.Abstractions` (BCL-only, §0.1). Gate everything below on it. |
| 1 | Build the engine in `smart-qr` (greenfield): `IMigrationRunner` / `IMigrationDevRunner`, `MigrationRunner`, sources, `PostgresMigrationTracker`, result types + `MigrationDriftException`, models, options, `MigrationContentNormalizer`, `OrdinalAllocator`. Prove against smart-qr's DB. |
| 2 | Build file-gen commands in smart-qr: `new` (flat Dev) / `promote` / `init` / `repair`. |
| 3 | ✅ **DONE (2026-06-13)** — copied the 22 engine `.cs` to `src/Data/Migrations/Bespoke/`; namespace rewrite `SmartQr.Common.Persistence.Migrations` → `WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke`. (CLI/HTTP adapters not extracted yet — engine only.) |
| 4 | **Enforce zero forbidden refs mechanically** (not eyeball): add an architecture test (NetArchTest / a custom Roslyn test) asserting the engine **source** imports no `Microsoft.AspNetCore.*`, `MediatR`, `System.CommandLine`, `Microsoft.Extensions.Hosting`, or domain namespaces; **and** a CI gate `dotnet list <web-free-engine/cli>.csproj package --include-transitive` asserting no `Microsoft.AspNetCore.*` / `MediatR`. The mono-lib folder inherits ASP.NET at the *csproj* level, so the **source-import** test is what guarantees web-freedom there; the transitive-package gate guards the CLI's web-free csproj. |
| 5 | ✅ **DONE (2026-06-13)** — re-pointed the injected factory `DbConnectionFactory` → `IDbConnectionFactory`; connection/transaction params across `MigrationRunnerService`, `IMigrationHistoryRepository`/`MigrationHistoryRepository`, `IMigrationDialect`/`PostgresMigrationDialect` are now BCL `DbConnection`/`DbTransaction`. The advisory-lock + transactional-DDL code works over `DbConnection` with **no downcast** (Dapper / BCL surface only). The lone genuinely-Npgsql site (`PostgresMigrationDialect.EnsureDatabaseExistsAsync`, §2.1.1) self-constructs its `NpgsqlConnection` and is untouched. |
| 6 | Wire packaging: add the standalone web-free engine/CLI csproj **outside** the mono-lib; CLI `ProjectReference` → `…Data.Abstractions` + engine sources, **never** the mono-lib. Add `System.CommandLine` / `Spectre.Console` / standalone `Npgsql` to `Directory.Packages.props` (CPM, §4.1.1). |
| 7 | **EF → pure mapper:** delete `EnsureCreated`; strip migration-only EF config per `database.md` waste-rule — remove `.IsRequired()`, `.HasMaxLength()`, `.HasColumnName()`, `.HasIndex()`, `.HasFilter()`, `.HasDatabaseName()`, `.HasPrecision()`; rely on `UseSnakeCaseNamingConvention()` for column names. Keep only runtime-effecting config (`.ToTable()`, `.HasKey()`, relationships, `.HasColumnType("jsonb")`, `.Ignore()`). |
| 8 | Add Testcontainers schema-conformance test: spin Postgres → run `apply` → assert EF round-trips **every** entity against the migrated schema. Assert (per step 7) configs carry no migration-only calls. |
| 9 | 🚧 **status DONE (2026-06-13)** — this doc's status is now **SHIPPING** and *Last updated* bumped. **Still TODO:** write the `../migrations.md` strategy index (Ef vs DbUp vs Sql). |

**Forbidden in the engine source** (asserted in step 4): domain types · `MediatR` · `Microsoft.AspNetCore.*` · `Microsoft.Extensions.Hosting` (runner only; the bootstrap adapter is the lone exception, excluded from the CLI build) · `System.CommandLine` · web config types.

---

## 7. Open decisions / parked items

| # | Item | Lean | Status |
|---|---|---|---|
| 1 | `…Data.Abstractions` package home (new org folder vs sdk-beta sibling) | sdk-beta sibling, BCL-only | **TODO (gates §6)** |
| 2 | `migrations.json` location — repo root vs per-product | repo root, env-interpolated | parked |
| 3 | `rebaseline` `information_schema` fallback fidelity (constraints/defaults it misses) | document gaps; warn loudly | **first-class** (§2.7) |
| 4 | `verify` without Testcontainers (CI image vs local DB) | require Testcontainers; skip with warning otherwise | parked |
| 5 | Advisory-lock contention on prod startup races (bounded wait?) | `pg_advisory_lock` blocks; add bounded wait + retry, tune ~30s | parked |
| 6 | Multi-statement `@no-transaction` files (each stmt autocommits) | keep single-statement; idempotent DDL mandatory (§2.5) | parked |
| 7 | `version` column semantics vs `ordinal` | `version` = free label (semver tag); `ordinal` = the gate | resolved |
| 8 | Embedded vs filesystem default per environment in `CompositeSource` | embedded base + filesystem overlay in dev | parked |
| 9 | CLI distribution: nuget.org vs internal feed for the tool package | follow SDK feed | parked |
| 10 | Where the HTTP endpoint host lives (SDK meta vs per-product) | per-product opt-in adapter | parked |
| 11 | Whether `Rollback.sql` / `@no-transaction` should join the checksum | currently excluded (§2.6 blind spot); revisit if it bites | parked |
| 12 | `../migrations.md` strategy index (Ef vs DbUp vs Sql) — missing | write when `Sql/` ships | **TODO** |

---

## See also

- `../Ef/` — EF Core code-first runner (`AddEfMigrationsRunner<TContext>`, DbContext-owned schema).
- `../DbUp/` — DbUp script runner (`AddDbUpRunner`, legacy option).
- `…Data.Abstractions` — **new** BCL-only home of `IDbConnectionFactory` (§0.1) the engine + CLI depend on.
- `…Data.EntityFrameworkCore` — EF as pure mapper over the SQL-owned schema.
- `conventions/development/backend/persistence/database.md` — schema-first rule + EF waste-rule (§5.1, §6 step 7).
- [Npgsql](https://www.npgsql.org/) · [Dapper](https://github.com/DapperLib/Dapper) · [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/)

