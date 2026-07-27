# 05 — Failure modes (adversarial pass)

*Research question 6 of [`data-pipeline-idea.md`](../data-pipeline-idea.md). Written 2026-07-22 to argue **against** the pipeline. Findings only — no design, no code.*

> Four sibling passes research how to build the layered EF + Dapper + cache pipeline. This one exists so a real objection surfaces now rather than after it ships. Every claim below is tagged **[code]** (verified against `src/` at this commit), **[doc]** (verified against a first-party doc, cited), or **[open]** (a stated risk that needs a test to settle).

---

## Verdict

**Build narrower.** Ship a **transaction-aware cache-invalidation seam** (problem 2 only) as an `IDbTransactionInterceptor`. Do **not** build the read→write handoff (problem 4), the request-scoped read cache (problem 3), or a general layered pipeline (problem 1).

**Single strongest argument:** the pipeline's foundational premise is factually wrong in this repo. The idea doc states *"shared `NpgsqlDataSource` consumed by both EF Core and Dapper, so they can share a connection."* They share a **pool**, not a connection — `DataSourceConnectionFactory.Create()` calls `dataSource.CreateConnection()`, which mints a *new* `NpgsqlConnection` every call, and `DapperRepository` opens and disposes one per operation **[code]** `src/Data/Abstractions/DataSourceConnectionFactory.cs:10,14`, `src/Data/Dapper/Repositories/DapperRepository.cs:49,58,68,77,86,100,110,126`. Two connections cannot be in one Postgres transaction. So problem 1 ("transaction scope is too narrow") is not narrow — it is **absent**: a Dapper write issued inside an EF transaction commits independently, today, silently. Every layer of the proposed pipeline inherits this. Fixing it requires changing the connection seam (`IDbConnectionFactory` returns an *unowned, ambient* connection + transaction), which is a breaking redesign of the thing the pipeline was going to be built *on top of* — and once that seam exists, `IUnitOfWork` gets you problem 1 without a pipeline at all.

Secondary: the SDK has **no `Data.Tests` project** **[code]** (`Foundation.Tests`, `Identity.Tests`, `Mediator.Tests`, `Messaging.Tests`, `Migrations.Tests`, `Web.Tests` exist; no `Data.Tests`, no BenchmarkDotNet anywhere). The layer that would carry the most subtle concurrency semantics in the SDK currently has zero automated coverage and zero measurement capability. Building a pipeline on it means the failure modes below are unfalsifiable at build time.

---

## 1. What a pipeline breaks that a plain repository doesn't

A repository call has one failure surface: the call throws or it doesn't. A pipeline adds four independent surfaces that a repository does not have. Enumerated, concretely:

1. **Ordering becomes load-bearing and invisible.** With repositories, "read the row, then write it" is two lines in one method. With a pipeline, order is a registration-time list assembled across DI extension methods in separate packages. The events layer already shows the shape: `EventProcessingPipeline.BuildChain()` composes `IEnumerable<IConsumeFilter>` with *first-registered = outermost* **[code]** `src/Messaging/Transport/EventProcessingPipeline.cs:93-103`. That ordering rule lives in a code comment, not in a type. Two packages that each register a filter now have an ordering dependency neither declares.
2. **Partial success becomes reachable.** A repository call either committed or threw. A pipeline can have layer 3 committed, layer 4 rolled back to a savepoint, and layer 2's cache write already flushed — a state with no name and no assertion.
3. **Side effects escape the failure domain.** A repository's only side effect is the SQL. A pipeline layer's side effects are SQL + a cache entry + an outbox row + in-memory entity mutations, each with a different durability boundary. The idea doc names the cache case; §4 shows the in-memory mutation case is worse and is *not* fixable by transaction-aware caching.
4. **Retry semantics stop being local.** `SaveChanges` retries are EF's business. A pipeline that spans layers must be re-executable as a unit, which means every layer must be idempotent — a constraint no current layer declares or is tested for. See F4.

---

## 2. Failure-mode table

| # | Scenario | What breaks | Sev | Detectable? |
|:--|---|---|:--:|---|
| **F1** | Dapper write (or read-for-update) issued while an EF transaction is open | Dapper is on a *different pooled connection* — it commits independently and is **not** rolled back with the EF transaction. Read-your-own-writes also fails: the Dapper connection can't see the EF transaction's uncommitted rows **[code]** | **Critical** | **No.** No exception, no log, no diagnostic. Only a data-consistency audit finds it. |
| **F2** | Inner-layer `SaveChanges` fails inside an outer transaction; caller catches and continues | EF **auto-creates a savepoint before every `SaveChanges` that runs inside an existing transaction and auto-rolls-back to it on error** **[doc]**. So the *transaction did not roll back* — only the savepoint did. Any cache compensation hooked to `TransactionRolledBack` never fires. The stale cache entry survives exactly as the idea doc fears, and the "transaction-aware cache" does not catch it. | **Critical** | **No.** Rollback-to-savepoint is a distinct EF event (`RolledBackToSavepoint`); a naive design won't subscribe to it. |
| **F3** | Same as F2, then the caller retries `SaveChanges` | `AppDbContextBase.IncrementConcurrencyVersions()` mutates `versioned.Version++` **in memory, before** `base.SaveChangesAsync` **[code]** `src/Data/EntityFrameworkCore/AppDbContextBase.cs:59-64`. A savepoint rollback does not undo a CLR field. Retry bumps it again → in-memory `Version` = DB + 2. The next update's concurrency predicate misses → spurious `DbUpdateConcurrencyException`, or (if `Version` is written as a value) a wrong token persisted. | **High** | Partially — surfaces as a "phantom" concurrency exception with no concurrent writer. Hard to attribute. |
| **F4** | Any explicit pipeline transaction on a context configured by `UseNpgsqlConventional` | `EnableRetryOnFailure(maxRetryCount: 6)` **[code]** `src/Data/EntityFrameworkCore/Postgres/PostgresExtensions.cs:24,44` makes `BeginTransaction` throw `InvalidOperationException: The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions` **[doc]**. The fix — `Database.CreateExecutionStrategy().ExecuteAsync(...)` — **re-runs the entire delegate** on a transient fault, replaying every layer's non-transactional side effects (cache writes, outbox enqueues, `Version++`, audit stamps). `VECTOR-ANALYSIS.md` §5.5 lists "AddPostgresPersistence drops `EnableRetryOnFailure`" as a **bug to fix** — so this collision is scheduled, not hypothetical. | **Critical** | **Yes at first** (loud exception). **No after the "fix"** — wrapping in the execution strategy silences the exception and converts it into silent double-application. |
| **F5** | Two `DbContext`s in one request (e.g. app context + Identity context) | They resolve **separate connections from the shared pool**. A transaction on one does not span the other. Sharing requires `context2.Database.UseTransaction(tx.GetDbTransaction())` **and** a shared `DbConnection` **[doc]** — neither is expressible through `AddPostgresPersistence`, which hands EF a `DbDataSource`, not a connection **[code]** `src/Data/PostgresPersistenceServiceCollectionExtensions.cs:51-58`. | **High** | **No.** Both saves succeed; atomicity is silently absent. |
| **F6** | Layered saves each auto-savepoint, held open (per-item error handling in a loop) | Postgres caches only `PGPROC_MAX_CACHED_SUBXIDS = 64` subtransactions per backend; past that the snapshot is `suboverflowed` and **every other backend** pays SLRU lookups in `pg_subtrans` **[doc]**. A cluster-wide latency cliff triggered by one request shape. (EF releases the savepoint on a successful save, so the normal path stays bounded — the cliff needs held-open savepoints.) | **High** | **Yes, but misattributed** — presents as global DB slowdown, not as "that endpoint". |
| **F7** | Cache invalidated/compensated on rollback, multi-instance deployment | *"When invalidating cache entries by key or by tags, they're invalidated in the current server and in the secondary out-of-process storage. However, **the in-memory cache in other servers isn't affected.**"* **[doc]** With `DefaultLocalCacheExpiration = 1 min` **[code]** `src/Caching/Hybrid/HybridCacheConventionOptions.cs:10`, every other node serves the rolled-back value for up to 60 s. The pipeline's headline promise degrades to "5-minute staleness becomes 1-minute staleness". | **High** | **No.** Node-local; invisible to the writing node. |
| **F8** | `RemoveByTagAsync` used as the compensation primitive | Tag invalidation is *logical*, not physical: it sets an "ignore anything created before this point" marker **[doc]**. Whether an entry written microseconds before the compensating call is reliably shadowed depends on timestamp granularity. | Med | **[open]** — needs a test. Assume unsafe until proven. |
| **F9** | Dapper-read entity reused/attached where EF semantics are expected | EF applies a global `!IsDeleted` query filter to every `ISoftDeletable` **[code]** `src/Data/EntityFrameworkCore/EntityModelConventions.cs:22-23`. `DapperRepository` emits bare `SELECT * FROM {Table} WHERE id = @id` **[code]** `:48,57` — **no filter**. A request-scoped read cache populated by Dapper serves soft-deleted rows to layers that assume they're invisible. This divergence exists **today**; the pipeline would institutionalize it and hide it behind a cache. | **High** | **No.** Repo docs claim the two repos are "interchangeable at the call site" (`Data/Dapper/Repositories/repositories.md`). |
| **F10** | Attaching a Dapper-materialized entity to EF | EF and Dapper have **two independent, unsynchronized conversion registries**: EF `ValueConverter`s (`EnumCaseConverter`, `JsonValueConverter`) vs Dapper `SqlMapper.TypeHandler`s (`EnumTypeHandler`, `DateOnlyTypeHandler`, `ListTypeHandler`) **[code]**. Nothing enforces parity. A property converted by EF with no matching Dapper handler materializes raw, then attaches as `Unchanged` — EF now believes the DB holds the raw value. Owned types are worse: `DapperRepository`'s column list is reflected public read-write props **[code]** `:16-21`; a flattened owned type maps to *no* column and materializes `null`. Attach + save → EF writes the owned columns as null. **Silent data loss.** | **Critical** | **No.** Attach is by definition "trust the caller's snapshot" — EF has no way to detect a lie. |
| **F11** | Any pipeline state (identity map, deferred cache buffer, enlistment list) stored on the `DbContext` | `AddEntityFrameworkCore` uses `AddDbContextPool` by default, `PoolSize = 1024` **[code]** `EntityFrameworkCoreServiceCollectionExtensions.cs:82-85`, `EntityFrameworkCoreOptions.cs:7,10`. Pooling resets the change tracker; it does **not** reset fields you add. State leaks across requests — cross-tenant/cross-user leakage in the worst case. Meanwhile `AddPostgresPersistence` uses plain `AddDbContext` **[code]** `:51` — so the same pipeline code is safe under one registration and leaks under the other. | **Critical** | **No.** Manifests as rare cross-request contamination under load. |
| **F12** | Pipeline implemented as an `AddEfInterceptor`-registered interceptor | `AddPostgresPersistence` — the flagship one-call registration — wires **only** `UseAuditInterceptor` and never reads `IEnumerable<IInterceptor>` **[code]** `:51-58`. The auto-wire loop lives in `AddEntityFrameworkCore` **[code]** `:66-68`, which `AddPostgresPersistence` doesn't call. Interceptors registered via `AddEfInterceptor` are **silently dropped** under the recommended registration. | **High** | **No.** No warning; the concern just doesn't happen. |
| **F13** | Long-running / streaming request with a per-request identity map | An identity map is an unbounded `Dictionary<key, entity>` keyed by everything the request touched. A streaming export over 10⁶ rows retains every row for the request's lifetime. EF's own tracker has this problem and the documented answer is `AsNoTracking` — a per-request map *outside* EF has no equivalent escape hatch and no eviction policy. Under `AddDbContextPool`, a leaked buffer also outlives the request (F11). | **High** | Partially — shows as memory growth / GC pressure, not attributed to the pipeline. |
| **F14** | Read replica for reads, primary for writes | Not expressible today. `IDbConnectionFactory` is a **singleton bound to one `DbDataSource`** **[code]** `ConnectionFactoryServiceCollectionExtensions.cs:23`, and `DapperRepository` takes it by plain (non-keyed) injection **[code]** `:30-34`. Routing reads to a replica requires a second registration the seam can't express. Adding it means: (a) replication lag breaks read-your-own-writes; (b) a cache populated from a *lagging replica* read, keyed as if authoritative, poisons every later layer including an attach — the attached "original values" are pre-write, so EF's concurrency predicate compares against a stale token and either spuriously fails or overwrites a newer row. | **High** | **No.** Lag-dependent → intermittent, unreproducible locally (single-node dev). |
| **F15** | Multi-tenant / multi-schema, or two contexts with different casing | `SqlNaming.ColumnCase` / `ParameterCase` are **static mutable globals** **[code]** `src/Data/Dapper/SqlNaming.cs:13,16`, and `AddDapperConventions` sets `DefaultTypeMap.MatchNamesWithUnderscores` + `SqlMapper.AddTypeHandler` **process-globally, once, guarded by an `Interlocked` latch** **[code]** `DapperServiceCollectionExtensions.cs:26-30`. Two configurations in one process is impossible; the second registration silently no-ops. | Med | **No** — the latch makes the second call succeed and do nothing. |
| **F16** | Distributed transactions across Postgres + Redis (L2 cache) | There is no two-phase commit here and there cannot be one: `System.Transactions` distributed transactions are **Windows-only since .NET 7** **[doc]**, and Redis isn't a resource manager. Any "transaction-aware cache" is *compensation*, not atomicity — there is always a window where the DB committed and the cache didn't, or vice versa. | **High** (as a design claim) | N/A — a design limit, not a bug. It must be *stated*, not solved. |

---

## 3. Nested transactions and savepoints

- EF Core does not nest transactions. A second `BeginTransaction` on a context with an active transaction throws; it is **not** silently converted to a savepoint. **[doc]**
- What EF *does* do implicitly is the F2 case: **`SaveChanges` inside an existing transaction creates a savepoint, and on error rolls back to it, "leaving the transaction in the same state as if it had never started."** **[doc]**
- On Postgres this is not a nicety — it is mandatory. Any error inside a transaction poisons it (`25P02: current transaction is aborted, commands ignored until end of transaction block`); a savepoint is the *only* way to continue **[doc]**. So an inner layer that "handles" a failure and continues is, on Postgres, always running inside a subtransaction whether it knows it or not.
- **The consequence the design must answer:** "rollback" is not one event. There are (at least) three, and they need three different cache dispositions:

| Event | Outer tx state | Correct cache action |
|---|---|---|
| `TransactionRolledBack` | dead | discard every enlisted write from every layer |
| `RolledBackToSavepoint` | **alive, committable** | discard only writes enlisted *after* that savepoint |
| `SaveChanges` throws, no user tx | nothing to roll back to; EF's implicit tx already gone | discard this save's writes |

A single "on rollback, compensate" hook gets the middle row wrong, and the middle row is the *most common* one in a layered design — that's what layering produces.

- **Cost:** each savepoint is `SAVEPOINT` + `RELEASE SAVEPOINT` — two extra round trips per layer save. Held-open savepoints past 64 in one backend trip the `suboverflowed` cliff and degrade the **whole cluster** **[doc]**.
- The repo already has one hand-rolled instance of implicit transaction bridging: `PostgresSkipLockedOutboxClaimStrategy` opens a transaction inside a claim call and bridges its commit onto the context's `SavedChanges` event, with an `Interlocked` settle flag and manual handler detach **[code]** `src/Messaging/Reliability/Ef/PostgresSkipLockedOutboxClaimStrategy.cs:39,64-100`. That is ~35 lines of subtle machinery for *one* implicit enlistment, and it works by **taking over the context's ambient transaction** — a pipeline that also opens one collides with it directly.

---

## 4. The failure transaction-aware caching does not fix

Both SDK save interceptors mutate **the caller's entity instances** during `SavingChanges`, before the write:

- `AuditInterceptor.Stamp` sets `UpdatedAt` / `UpdatedBy` on the live object **[code]** `src/Data/EntityFrameworkCore/Audit/AuditInterceptor.cs:42-56`
- `SoftDeleteInterceptor.Soften` sets `IsDeleted` / `DeletedAt` / `DeletedBy` and flips `EntityState` **[code]** `src/Data/EntityFrameworkCore/SoftDelete/SoftDeleteInterceptor.cs:39-63`
- `AppDbContextBase.IncrementConcurrencyVersions` bumps `Version` **[code]** `:59-64`

None of these mutations are reversed by a transaction rollback or a savepoint rollback — CLR fields aren't transactional. The pipeline's core value proposition is *"read once, share the materialized entity across layers"*. The instant you share the instance, a failed write **poisons the shared read** with values that were never committed: an `UpdatedAt` from a rolled-back attempt, a `Version` that's ahead of the row, an `IsDeleted = true` on a delete that rolled back.

This is the idea doc's problem 2 — but at the object level, inside the request, where no cache hook can reach it. **A transaction-aware cache solves the smaller half of the problem the pipeline creates.** The sharing is what creates it; the sharing is the pipeline's premise.

---

## 5. Connection lifetime — verified

| Question | Answer **[code]** |
|---|---|
| Do EF and Dapper share a connection? | **No.** Same `NpgsqlDataSource` (= same pool), different `NpgsqlConnection` objects. `DataSourceConnectionFactory.Create()` → `dataSource.CreateConnection()`; EF gets its own via `UseNpgsql(dataSource)`. |
| How long does Dapper hold one? | One operation. `DapperRepository` opens and `await using`-disposes a connection **per method call** — 8 call sites, 8 independent connections for 8 calls. |
| Does a Dapper command enlist in EF's transaction? | **No.** Nothing passes `IDbTransaction` to any `CommandDefinition`; nothing calls `GetDbConnection()` / `GetDbTransaction()` anywhere in `src/Data/`. |
| Can it be made to? | Only by changing the seam: `IDbConnectionFactory` must be able to return the *ambient, unowned* connection + its transaction, and `DapperRepository` must stop disposing it. That's a breaking change to a shipped public interface. |
| Is there a `IUnitOfWork` / transaction abstraction to build on? | **No.** Zero `BeginTransaction` in `src/Data/` outside the migrator. `VECTOR-ANALYSIS.md` §2.3 rates unit-of-work **1/5**: *"every repo write auto-`SaveChanges`; multi-aggregate atomicity is hand-rolled."* Confirmed at `EfRepository.cs:50,59,67,75,86`. |

**Implication:** the pipeline cannot be layered *over* the current repositories, because `EfRepository` commits on every call. A pipeline that wraps them wraps N committed transactions, not one. The repositories must change first — and once they do, the pipeline's problem 1 is already solved by whatever made them change.

---

## 6. Debuggability — is the events-layer trade acceptable twice?

The events layer paid this cost once, deliberately, and the trade was defensible there: message consumption is **already** asynchronous and already detached from a caller's stack. There is no stack trace to lose — the trace was reconstructed from OTel spans (`MessagingDiagnostics.Source.StartActivity`, parent-context extraction from headers) **[code]** `src/Messaging/Transport/EventProcessingPipeline.cs:41-47`. A filter chain is *cheaper* than nothing there.

Data access is the opposite case:

- A repository call today has a **complete synchronous stack** from controller → handler → repository → Npgsql, with the SQL in the exception.
- A pipeline replaces that with `Layer1.InvokeAsync → Layer2.InvokeAsync → … → core`, where every frame is the same lambda closure shape (`(ctx, token) => filter.InvokeAsync(ctx, next, token)` **[code]** `:99`) and the interesting state — which layer enlisted which cache key, which savepoint is current — lives in a context object, not the stack.
- **The events layer converts an already-lost trace into a partially-recovered one. A data pipeline converts a fully-present trace into a partially-recovered one.** Same mechanism, opposite sign.

The specific loss: F1, F2, F5, F7, F9, F10, F11, F12, F14 are all in the **"detectable? No"** column. A layered pipeline's characteristic bug is *nothing happened where something should have* — no exception, no stack, no log line. Stack traces don't help with omissions; only assertions do, and there is no `Data.Tests` project to put them in.

---

## 7. The magic tax

Three implicit behaviors are proposed. Priced individually:

| Magic | What a developer must know to debug it | Cost when the abstraction is wrong |
|---|---|---|
| Implicit enlistment | which layers registered, in what order, which opened a transaction vs a savepoint, whether the execution strategy is replaying (F4) | F1/F5: writes silently outside the transaction. The *absence* is invisible. |
| Implicit caching | which layer owns which key, whether this read came from L1 / L2 / DB / a replica (F14), whether a tag invalidation has shadowed it (F8), whether another node's L1 is stale (F7) | Serving uncommitted or lagged data. Reproduces only under multi-node load. |
| Implicit attach | which columns Dapper actually selected, which EF converters have no Dapper counterpart (F10), whether owned types materialized, whether the concurrency token is real or default | F10: silent data loss on save. |

**The comparison the doc should make honestly:** the alternative to all three is *writing the two queries by hand*. That costs one extra `SELECT` (~0.2–1 ms on a primary-key lookup over a warm local connection) and produces code where the failure is a stack trace. The pipeline costs a permanent, ecosystem-wide obligation to keep three registries in sync (EF model ↔ Dapper handlers ↔ cache keys), and its failures are the undetectable kind.

The SDK's own doctrine — *"the real cost is integration, paid once in the SDK"* — assumes the integration is **correct once**. Here the integration is a *continuous* correctness obligation: every new entity, converter, owned type, or query filter re-opens F9 and F10.

---

## 8. Performance — the round-trip premise

The idea doc's problem 4 assumes the second read is the cost. Test that assumption:

- **What's actually saved:** one `SELECT … WHERE id = $1` on a warm pooled connection. On the local/regional Postgres these products run against, that's roughly **0.2–1 ms** including a planned index scan. **[open]** — unmeasured here; stated as an order of magnitude, not a figure.
- **What's actually paid:**
  - The savepoint EF creates around a `SaveChanges` inside an existing transaction — `SAVEPOINT` + `RELEASE SAVEPOINT` = **two extra round trips per layer save** **[doc]**. A 3-layer pipeline that saves twice pays *more* round trips than the read it eliminated.
  - Serialization on every L2 cache read/write (`System.Text.Json` by default **[doc]**), plus a Redis round trip on an L1 miss — plausibly **slower** than the Postgres PK lookup it replaced.
  - `HybridCache` deserializes per call by default to preserve `IDistributedCache` thread-safety semantics; instance reuse requires the type be `sealed` + `[ImmutableObject(true)]` **[doc]** — which SDK entities are not (they're mutable by contract: `IAuditable`, `IVersioned`, `ISoftDeletable` all have setters **[code]**). So the cached-entity path pays a full deserialize per read *and* hands out a different instance than the tracker holds.
- **Attach vs. tracked query:** attaching skips the query but costs `DetectChanges`-equivalent work over the attached graph, and it *forfeits EF's original-values snapshot* — the thing that makes the concurrency predicate and the minimal `UPDATE … SET` column list correct. An `Update()` on an attached entity marks **every** property modified unless original values are supplied. That means wider `UPDATE` statements, more WAL, and more lock contention than a tracked read + targeted update.

**Can the claim be measured today? No.** There is no `Data.Tests` project and no BenchmarkDotNet harness in the repo **[code]** (`VECTOR-ANALYSIS.md` §2.5 lists "BenchmarkDotNet perf harness" as *not yet built*). **The premise "avoid a round trip" is currently unfalsifiable in this codebase and, on the reasoning above, is more likely inverted than confirmed.** Anything built on it is built on an unmeasured assumption.

---

## 9. The simpler answer, per problem

| # | Idea-doc problem | Simplest thing that solves it | Why it's enough |
|:--|---|---|---|
| **1** | Transaction scope too narrow | **`IUnitOfWork`** — an explicit scoped seam owning one `DbConnection` + one `DbTransaction`; `IDbConnectionFactory` returns the ambient connection when one is open; repositories stop calling `SaveChanges`. | Already the repo's own #3 improvement for Data (`VECTOR-ANALYSIS.md` §2.3, unit-of-work rated **1/5**). Explicit, one type, greppable, one place to put the execution-strategy wrapper (F4). Solves F1 and F5 — which a pipeline over today's repositories does **not**. |
| **2** | Cache coherence under rollback | **`IDbTransactionInterceptor`** — a first-party EF seam with `TransactionCommitted` / `TransactionRolledBack` / `CreatedSavepoint` / `RolledBackToSavepoint` / `ReleasedSavepoint` hooks **[doc]**, plus a scoped enlistment list keyed by savepoint depth. | EF **already ships** the transaction-lifecycle interception the pipeline wants to invent. `VECTOR-ANALYSIS.md` §5.3 already names the interceptor as the reusable template: *"cache-invalidation-on-save … Don't invent new plumbing."* Note: an `ISaveChangesInterceptor` is **not** sufficient — `SavedChanges` fires at savepoint-release time, not commit time (F2). |
| **3** | Duplicate validation reads | **Don't validate twice.** Validate once, at the presentation/mediator boundary, and let persistence enforce invariants with DB constraints (unique index, check, FK) mapped through the existing `DbExceptionMappingRule` → `AppError` seam **[code]** `src/Data/Errors/`. | The duplicate read is a symptom of duplicated *responsibility*, not of a missing cache. Caching the read makes both validations permanent and adds F7/F8/F9. If both layers genuinely must validate, pass the already-loaded entity down as a **parameter** — an argument, not an ambient cache. |
| **4** | Dapper read → EF write handoff | **Don't hand off. Read with EF on the write path.** Dapper is for the read *model* (projections, list queries, reports); the write path reads its aggregate with EF and gets tracking, original values, converters, owned types, and query filters for free. | Removes F9, F10, F14's attach hazard, and the entire EF-vs-Dapper conversion-parity obligation. Costs one PK lookup (§8) — which is very likely cheaper than the savepoint + serialization it replaces. |

---

## 10. The narrowest version worth building

**Build:** `AddTransactionalCacheInvalidation()` — one `IDbTransactionInterceptor` + one scoped enlistment buffer.

**Contract:**
- A layer calls `cacheScope.InvalidateOnCommit(key)` / `SetOnCommit(key, value)`. Nothing touches `ICache` before commit.
- On `TransactionCommitted` → flush the buffer.
- On `TransactionRolledBack` → drop the buffer.
- On `CreatedSavepoint` → mark the buffer position. On `RolledBackToSavepoint` → truncate to that mark. On `ReleasedSavepoint` → merge into the parent frame. **This is the part a naive design omits and F2 is why it can't be omitted.**
- No open transaction → the call is a straight passthrough to `ICache` (matches today's `SaveChanges`-per-write reality).
- **Prefer invalidate-on-commit over set-on-commit.** Invalidation is idempotent and survives F4's replay and F7's cross-node L1 gap (next read repopulates from the DB). A deferred `Set` re-introduces the write-skew the whole exercise is trying to remove.

**Ships with, non-negotiable:**
1. A `Data.Tests` project. Testcontainers Postgres. Cases: commit-flush, rollback-drop, savepoint-truncate, nested-savepoint-merge, no-transaction passthrough, execution-strategy replay.
2. A documented limitation section stating F7 (other nodes' L1 is not invalidated — bounded by `DefaultLocalCacheExpiration`, 1 min) and F16 (compensation, not atomicity).
3. The F12 fix — `AddPostgresPersistence` must run the `IEnumerable<IInterceptor>` auto-wire loop, or the interceptor silently never runs.

**Explicitly excluded:**
- Any Dapper→EF attach (F10 — silent data loss, no detection path)
- A request-scoped read cache / identity map (F13 unbounded growth, F11 pool leakage, F9 soft-delete divergence)
- A general layered read/write pipeline (§1, §6, §7 — the debuggability trade inverts vs the events layer)
- Cross-`DbContext` or Dapper transaction enlistment **until** `IUnitOfWork` exists (F1, F5) — and if `IUnitOfWork` gets built, it should be built and shipped **as `IUnitOfWork`**, not as a pipeline

**Sequencing:** `IUnitOfWork` (solves problem 1 alone, unblocks everything) → this interceptor (solves problem 2) → **stop and re-ask** whether problems 3 and 4 still exist. On the analysis above, problem 3 dissolves under "validate once" and problem 4 dissolves under "read with EF on the write path". If they survive that, the case for them will be made from a real product's profile, not from a premise (§8) that the repo cannot currently measure.

---

## 11. Not verified — settle before designing

| Claim | Status |
|---|---|
| `RemoveByTagAsync` timestamp granularity vs a same-tick cache write (F8) | **[open]** — needs a test against `Microsoft.Extensions.Caching.Hybrid 10.1.0`. |
| Actual PK-lookup latency vs savepoint + serialization cost (§8) | **[open]** — unmeasurable today; no benchmark harness exists. |
| Whether `IDbTransactionInterceptor` hooks fire for EF's **implicit** `SaveChanges` transaction (not just user-initiated) | **[open]** — the docs list the hook categories but do not state this. Determines whether the no-transaction passthrough in §10 is needed or is dead code. |
| Whether EF's auto-savepoint is ever skipped on Npgsql (the documented skip is SQL Server MARS) | **[open]** — assumed always-on for Npgsql; verify, because F2's whole shape depends on it. |

---

## Sources

- [Transactions — EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions) — auto-savepoint on `SaveChanges` inside a transaction; cross-context `UseTransaction` requires a shared `DbConnection` **and** `DbTransaction`; `System.Transactions` distributed support is .NET 7+ **Windows-only**
- [Connection Resiliency — EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) — retrying execution strategy vs user-initiated transactions; `CreateExecutionStrategy()` replays the delegate
- [Interceptors — EF Core](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors) — `IDbTransactionInterceptor` covers creating/committing/rolling back transactions **and creating and using savepoints**
- [HybridCache library in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) — cross-node L1 is **not** invalidated; tag invalidation is logical, not physical; per-call deserialization unless `sealed` + `[ImmutableObject(true)]`
- [ERROR 25P02: Current Transaction is Aborted — Postgres](https://www.bytebase.com/reference/postgres/error/25p02-current-transaction-is-aborted/) · [Cybertec: current transaction is aborted](https://www.cybertec-postgresql.com/en/error-current-transaction-is-aborted-in-postgresql/) — savepoints are the only way to continue a transaction after an error
- [PostgreSQL subtransactions considered harmful — PostgresAI](https://postgres.ai/blog/20210831-postgresql-subtransactions-considered-harmful) · [Why we spent the last month eliminating PostgreSQL subtransactions — GitLab](https://about.gitlab.com/blog/why-we-spent-the-last-month-eliminating-postgresql-subtransactions/) · [PostgreSQL Docs 18 §67.3 Subtransactions](https://www.postgresql.org/docs/current/subxacts.html) — `PGPROC_MAX_CACHED_SUBXIDS = 64`, `suboverflowed` snapshots, cluster-wide SLRU contention
- [Can we share connection between `DbContext`s using shared `NpgsqlDataSource`? — npgsql/efcore.pg#3053](https://github.com/npgsql/efcore.pg/issues/3053) — a shared data source is a shared *pool*, not a shared connection
