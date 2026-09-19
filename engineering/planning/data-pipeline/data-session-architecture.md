# Data session — architecture

*2026-07-19 · Synthesis of 4 verified component designs + adversarial cross-check (14 contradictions resolved). Research base: [`research/01–05`](./research/), verdict: [`data-pipeline-verdict.md`](./data-pipeline-verdict.md). Design decisions below are post-resolution — where lanes disagreed, the verifier's resolution is what's recorded.*

## Fixed constraints (owner)

Reads stay Dapper (complex multi-entity queries) · partial updates deferred · caching plugs in later through the hook seam · no filter chain — registration list on the transaction · soft-delete configurable per entity · tenant predicates come from the ambient server-owned tenant; no tenant is an explicit system/admin scope.

---

## 1 · `IDataSession` + `IDataUnit` (fixes D1, D6)

- `IDataSession` — DI-**scoped**, lazy, passive: `Idle → Active → Committed/RolledBack`. Root-provider resolution throws by construction.
- `IDataUnit` = the rollback boundary: `await using var unit = await session.BeginAsync(); … await unit.CompleteAsync();` — dispose without complete = rollback.
- Nested `BeginAsync` while Active → savepoint-backed unit (depth+1); `AbandonAsync` rolls back to the savepoint, outer stays committable; only the outermost `CompleteAsync` issues COMMIT.
- **Who opens it**: mediator `DataUnitBehavior` (thin wrapper — success result → complete, failure/exception → abandon). Jobs / outbox / sagas / consumers already own DI scopes → resolve + `BeginAsync` explicitly. The behavior is never the owner.
- **Connection**: EF-first — `Database.BeginTransactionAsync()`, session exposes `GetDbConnection()` + `GetDbTransaction()`. Works identically under `AddDbContext` and `AddDbContextPool`. One primary `DbContext` per session; second context deferred.
- **Dapper joins via a lease**: new scoped seam (`ISessionConnectionLease`) consumed by `DapperRepository` — returns the session's connection+transaction (`OwnsConnection=false`) while a unit is Active, else falls back to the singleton `IDbConnectionFactory`. **Do not replace the singleton factory** — `MigrationRunnerService` is a singleton consumer (captive-dependency break), and the migrator must never join a session.
- Custom `IDbConnectionFactory` that can't join while a unit is Active → **log/throw, never silently degrade** (silent degrade re-creates D1 behind a flag).
- Singleton-interceptor → scoped-session bridge: `ConditionalWeakTable<DbTransaction, SessionCore>` keyed by the transaction instance — pooling-proof. No AsyncLocal.
- **Commit timing** (4 models): `PerUnit` (default with a unit open; writes flush per-call inside the tx, COMMIT at complete) · `PerWrite` (no unit = today's behavior, escape hatch) · nested savepoint units · `Manual`. Session state decides at the repository — commit-timing options are read at the boundary only.
- `EnableRetryOnFailure`: execution strategy + session units validated at `AddDataSession`/boot — the strategy-replay collision (research 05) is forbidden config, not a runtime surprise.

## 2 · Hook registry (the future-cache seam)

- One registry, one interceptor. `IDataSession.Hooks`: `OnCommitted(action, dedupKey?)` · `OnRolledBack(Func<RollbackScope,…>)`.
- Frames are **depth-tagged**; rollback-to-savepoint discards that frame's intents. `dedupKey` collapses into the **shallowest** frame (else savepoint truncation under-invalidates).
- **Drain point**: the session drains after its own `CommitAsync` returns — sidesteps both the `WhenNeeded` trap (`TransactionCommitted` never fires for single-command saves) and the `SavedChanges`-fires-before-commit trap.
- No session active → `OnCommitted` runs **inline immediately** (Spring's shape; matches SaveChanges-per-write reality, no interceptor dependency).
- Hooks are isolated per-hook (one throwing never skips the rest — messaging-observer precedent), at-least-once, idempotent-by-contract. `DrainTimeout` logs + marks failed intents on a counter.
- Cache adapter (later) sits on exactly this: **eviction-only intents**, keys owned by the registering layer. Optional durability via `EfOutbox` explicitly deferred.

## 3 · Dapper read hardening (fixes D2, D3)

**Two read modes, distinguished by return type — not by a flag.** The type is compiler-checked; a bool parameter isn't.

| Mode | Returns | Attached? | Feeds |
|---|---|---|---|
| display / read flow (**default**) | `TEntity` or DTO | never | read endpoints, projections, multi-entity joins |
| read-for-update (opt-in) | `FullRow<TEntity>` | by the seam, never by the caller | the write path only |

- a plain read can never reach `UpdateAsync` — it doesn't type-check
- `FullRow<T>` is minted only by a full-column read (all mapped columns + `xmin`)


- **`EntityReadProfile`** — the single per-entity registry (session's duplicate config type deleted), **projected from EF model metadata** (one source of truth): soft-delete on/off, tenant column, tenant-missing policy, column list, `HasXmin`.
- **Fragment composition** for the primary read shape (hand-written multi-entity SQL): `SqlRead.Filters<T>()` / `SqlRead.Columns<T>()` compose into caller SQL — decorators can't inject into hand-written queries; Dapper has no interception seam. Generated CRUD reads get predicates automatically.
- **Tenant: fail-closed** on the Dapper path (no ambient tenant → throw). D2 is a security bug; fail-open is the EF filter's shipped behavior — asymmetry flagged to owner (below).
- **xmin**: explicit column lists with `xmin` aliased (`SELECT *` never returns it — D3); `Columns<T>()` carries it for hand-written SQL.
- `DataReadOptions` (canonical shape, dapper lane's): tenant bypass · include-soft-deleted · lock (`ForWrite()` = FOR UPDATE + requires session tx) · consistency (`JoinSessionIfOpen` default / `RequireSessionTransaction` / `Autonomous`). Per-call **read** overloads allowed on repositories; **write** options never appear on signatures.
- `FullRow<T>` — a completeness-marked wrapper minted only by full-column reads; the write path's only Dapper input (below).

## 4 · Write-path guards (fixes D4) + sanctioned update flow

- **Sanctioned flow**: fetch full previous → `Attach` (Unchanged baseline) → `CurrentValues.SetValues(incoming)` → save ⇒ **diff-only UPDATE**. `Update()` / `State = Modified` banned — they flag every column and write defaults over real data.
- **`LoadForUpdate<T>(id)` owns the order** — the caller never holds an unattached entity, so mutate-then-attach is unreachable. This is structural, not a rule: **Rule A cannot catch it** (mutate-then-attach leaves the entity `Unchanged`, not `Modified`), and `SaveChanges` then returns success having written nothing. Measured — smart-qr POC probe 2.
- **Dapper reads need EF's `ValueConverter`s applied before attach** (jsonb, owned columns — POC probe 6). Converters are **reflected from the EF model**, never hand-maintained: smart-qr already carries 2 jsonb converters and a hand-map drifts silently.
- Prev-version source: **`FindAsync` default** (identity-map hit = 0 SQL) · `FullRow<T>` via Dapper = gated opt-in (`PreviousFrom` in per-entity write config) · pessimistic `ForWrite()` = opt-in escalation for hot aggregates.
- `UpdateAsync(FullRow<TEntity> previous, TEntity incoming)` — the 2-arg overload takes `FullRow`, not bare entity, so provenance is compiler-checked (EF-loaded previous gets its `FullRow` minted by the repository itself).
- **Rule A** (runtime backstop): `ChangeTracker.Tracked` — entry enters tracking `Modified && !FromQuery` → throw. Catches every hand-rolled `Update()` on an untracked instance. **Must re-subscribe per pool rent** — P2 measured the subscription cleared on pool return, so arm-once leaves the guard live in dev and silently dead from request 2 in prod.
- **Rule B**: concurrency-token **OriginalValue** at CLR default on a tracked-for-write entity → throw. The *incoming* DTO's token is ignored — server-side token authority; `Xmin=0` on incoming is the normal case, the attached previous carries the real token.
- `IVersioned` seed moves to 1 (version-0 rows silently match an unfetched token).
- `AutoDetectChangesEnabled=false` silently disables audit/soft-delete/version interceptors → asserted loudly (bulk-insert carve-out for Added-only trackers).
- **D5 fix folded in**: audit + soft-delete wiring migrates onto the `AddEfInterceptor` auto-wire loop, ordering = DI registration order, guarded by a startup validation + regression test. (`AddPostgresPersistence` currently drops loop-registered interceptors; guard ordering is impossible under shipped wiring.)

---

## Owner rulings

1. **Tenant scope:** EF and generated Dapper CRUD align on ambient tenant predicates. No ambient tenant is reserved for explicit system/admin work; hand-written SQL owns its predicate.
2. **Idempotency ordering:** once the session exists, persist successful idempotency responses through `OnCommitted`; a rolled-back command must not leave a cached success.
3. **Breaking changes:** approved for the beta SDK. Document the final repository/session migration when the vector ships.

## Load-bearing probes — **measured 2026-07-24**, now permanent tests in `Data.Tests`

| # | Question | Measured answer |
|---|---|---|
| P1 | Do `CreatedSavepoint`/`RolledBackToSavepoint` fire for EF's **automatic** per-`SaveChanges` savepoints on Npgsql? | **Yes, both.** EF 10.0.3 / Npgsql 10.0.0. Per save: `created → (rolled-back-to on failure) → released`. **EF also releases *after* rolling back** — the depth model must not read a released frame as surviving. Depth counter can ride the events. |
| P2 | Does a `ChangeTracker.Tracked` subscription survive `DbContextPool` return+rent? | **No.** Pool returns the *same instance* with `Tracked` cleared: 1 fire on rent 1, **0** on rent 2. **Rule A cannot be armed once per instance** — armed in dev, silently off from request 2 in prod. Must re-subscribe per rent (verified: 3 rents → exactly 3 fires, no drops, no double-count). |
| P3 | HybridCache tag invalidation cross-node? | Out of suite — cache-adapter phase. Drain stays key-based so the answer can't silently change behaviour. |
| P4 | Outbox claim strategy vs a session unit on the same context | **Collision confirmed, fails loudly.** `ClaimPendingAsync` throws at its own `BeginTransactionAsync`. It throws *before* attaching its `SavedChanges` commit bridge, so the caller's unit still rolls back whole — no corruption. Dispatcher must skip units, or the strategy must join one. |

## Build order

| Phase | Delivers | Fixes |
|---|---|---|
| 0 | interceptor wiring unification + startup validation · **`Data.Tests` + harness deltas** (non-negotiable) · probes P1–P4 | D5, **D7** |
| 1 | `EntityReadProfile` + fragment composition + fail-closed tenant + xmin columns | D2, D3 |
| 2 | `IDataSession`/`IDataUnit` + lease seam + hook registry + `DataUnitBehavior` | D1, D6 |
| 3 | write guards (Rules A/B) + `LoadForUpdate<T>` + converter reflection + `FullRow<T>` | D4 |
| later | cache adapter over hooks · partial updates · second `DbContext` · Marten mining pass |

Test plan: [`data-session-test-plan.md`](./data-session-test-plan.md) — 105 cases, suite decision, determinism traps.
POC (measured, runnable): `ventures/smart-qr-poc/engineering/research/data-seam/` — 6 probes; 2 and 6 are load-bearing here.
Consumer-side convention (validation phases, lane split): smart-qr `conventions/.../validation.md` § *Phases*.

Phase 1 is independent of 2/3 and security-first — it can ship alone. Phase 3 depends on 2 (session state drives save timing) and on P2.
