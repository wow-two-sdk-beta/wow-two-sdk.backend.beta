# 04 — Request-scoped read cache

*Researched 2026-07-22. Answers open question **4** and problem **3** (duplicate reads) of [`data-pipeline-idea.md`](../data-pipeline-idea.md). Findings only — no design, no code.*

> Every behavioural claim marked **(P#)** was verified empirically against **EF Core 10.0.10** (SQLite provider, two named global query filters shaped like the SDK's soft-delete + tenant filters). Probe list and raw output in the [appendix](#appendix--probe-results).

---

## Verdict

**Use EF's identity map. Do not build a second store for entities. The largest single win is a one-line repository change.**

| Read kind | Cache | Why |
|---|---|---|
| entity fetched **by key** | EF change tracker, reached via `Find`/`FindAsync` | it *is* a request-scoped identity map, already reset correctly by scope and by the pool |
| entity fetched by **query** (list, filter, page) | nothing — tracking dedups instances, not queries | the map is keyed by PK; a query still executes |
| **non-entity** read model (Dapper projection, aggregate, count) | a separate per-scope memo is legitimate | EF will never track these, so no conflict is possible |

Three findings drive that verdict:

- `EfRepository.GetByIdAsync` uses `Set.FirstOrDefaultAsync(e => e.Id.Equals(id))` (`src/Data/EntityFrameworkCore/Repositories/EfRepository.cs:31`). That **never consults the identity map** — it queries every time. Switching it to `FindAsync` makes the second validation layer's read free **(P1)**. That is the whole of problem 3 for anyone on the EF repository, for one line.
- The CQRS split binds `IReadRepository` to **Dapper** (`src/Data/CqrsRepositoryServiceCollectionExtensions.cs:33-34`), and Dapper has no map. So "just use tracking" only closes problem 3 once the previous-version read either moves onto EF **or** the Dapper result is attached (question 1's job) — after which the second layer's `Find` costs 0 round trips **(P6)**.
- A second identity map over entities is not merely redundant, it is **illegal**. EF throws `InvalidOperationException` when a second instance with the same key is attached **(P7)**. Two stores that can each hand out an instance for the same key is exactly the state EF forbids.

So the plain answer to *"is this just tracking?"* — **yes for entities, no for read models.** The new thing worth building is a per-scope memo for **query results that aren't entities**, plus an invalidation channel. Not an identity map.

---

## What this repo does today

| Fact | Where |
|---|---|
| `IReadRepository<TEntity,TId>` — `GetById` · `GetAll` · `Exists` · `Count`. Only `GetById` is map-eligible | `src/Data/Abstractions/IReadRepository.cs:13` |
| CQRS split: reads → Dapper, writes → EF | `src/Data/CqrsRepositoryServiceCollectionExtensions.cs:33-34` |
| Dapper `GetByIdAsync` opens a **fresh connection per call**, `SELECT * FROM t WHERE id = @Id` — no map, no tenant predicate, no soft-delete predicate | `src/Data/Dapper/Repositories/DapperRepository.cs:46-52` |
| EF `GetByIdAsync` → `FirstOrDefaultAsync`, not `FindAsync` — bypasses the map | `src/Data/EntityFrameworkCore/Repositories/EfRepository.cs:31` |
| `DeleteByIdAsync` re-reads through the same non-map path | `src/Data/EntityFrameworkCore/Repositories/EfRepository.cs:81` |
| `NoTrackingByDefault` defaults **false** (good — see P4); `UsePooling` defaults **true**, pool 1024 | `src/Data/EntityFrameworkCore/EntityFrameworkCoreOptions.cs:7,19` |
| `AddPostgresPersistence` registers `AddDbContext` (**not** pooled) over a shared `NpgsqlDataSource` used by both EF and Dapper | `src/Data/PostgresPersistenceServiceCollectionExtensions.cs` |
| Mediator is **transient** and resolves handlers from the provider it was injected with — it creates no scope of its own | `src/Mediator/Mediator.cs:9`, `src/Mediator/MediatorServiceCollectionExtensions.cs:23` |
| Tenant isolation is an EF **query filter** closing over a singleton `AsyncLocal` context | `src/Tenancy/PerRow/TenantModelBuilderExtensions.cs:46`, `src/Tenancy/Core/AmbientTenantContext.cs:10` |
| Soft delete is an EF **query filter** | `src/Data/EntityFrameworkCore/SoftDelete/SoftDeleteModelBuilderExtensions.cs` |
| `CacheKeyBuilder.Build(params string[])` — `string.Join(':')`; nothing forces a tenant segment in | `src/Caching/Core/CacheKeyBuilder.cs:7` |

Two consequences fall straight out of that table:

- **the Dapper read path is filter-blind.** Tenant and soft-delete are enforced *only* by EF query filters. `DapperRepository` emits no such predicate for any of its four read methods. That is a standing hazard independent of this research question, and it is the mechanism by which a foreign row can reach EF's map (see [tenant leak](#the-tenant-leak-hazard)).
- **duplicate reads are structural, not accidental.** Two layers calling `IReadRepository.GetByIdAsync` today means two `SELECT`s on two connections, and no shared state anywhere can prevent it.

---

## Can EF's identity map be populated from outside a query?

Yes, and it is the intended seam.

| API | What it does for a read cache |
|---|---|
| `Attach(entity)` | inserts the instance into the map as `Unchanged`. Later `Find` returns *that instance*, 0 SQL **(P6)** |
| `AttachRange` | same, batched |
| `ChangeTracker.TrackGraph(root, cb)` | walks a graph and lets the callback pick a state per node — the seam for a Dapper multi-mapped graph. State/partial-entity constraints belong to question 1 |
| `Entry(e).State = ...` | single-node equivalent of `TrackGraph` |
| `DbSet<T>.Local` | a **view over** the map for one type — `Local.Count`, enumerate without querying |
| `DbSet<T>.Local.FindEntry(key)` | **map-only lookup with no DB fallback** **(P13)** — the primitive for "already cached?" without the query side-effect `Find` has |

Hard invariant, and it is the reason a second store is out of the question:

> **One instance per (entity type, key) per context.** A second `Attach` or `Update` with the same key throws `InvalidOperationException: The instance of entity type 'X' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked.` **(P7)**

A cache miss is a performance event. A double-materialization is an **exception**. Any pipeline that lets two layers independently materialize the same row must funnel them through one owner.

*Attach legality for partially-selected entities, concurrency tokens, shadow properties, owned types and navigations is question 1's subject — see the sibling research doc for that half.* One partial-attach result matters **here** and is recorded below under staleness.

---

## `Find` / `FindAsync` — does it get this for free?

Mostly yes. Verified semantics:

| Behaviour | Result |
|---|---|
| entity already tracked | returned immediately, **0 SQL**, same reference **(P1)** |
| not tracked | queries by PK, tracks the result — the next `Find` is free **(P1)** |
| global query filters on the **DB path** | **applied** — both the soft-delete filter and the tenant filter excluded their rows **(P2)** |
| global query filters on the **map path** | **bypassed entirely** — no query runs, so no filter runs **(P3)** |
| entity in `Added` state (not yet saved) | returned from the map, 0 SQL **(P8)** |
| context-wide `QueryTrackingBehavior.NoTracking` | `Find` neither reads nor populates the map — **every call re-queries** **(P4)** |
| a prior `AsNoTracking()` read | map untouched; the following `Find` still hits the DB **(P5)** |
| `AsNoTrackingWithIdentityResolution()` | identity map is **per query**, not per context — two such reads return two instances **(P14)** |

**(P2) is worth flagging** because the widespread belief is the opposite. On EF Core 10 with `HasQueryFilter`, `Find`'s fallback query goes through the `DbSet` query root and *does* honour global filters. The filter gap is **(P3)** — the map path — not the query path.

What `Find` does **not** give:

- nothing for `GetAll` / `Count`; `Exists` could be answered from the map first but isn't today
- no dedup for anything but a PK lookup
- nothing at all for Dapper

---

## Scope and lifetime

**The unit is the DI scope, not the HTTP request.** Every execution context in this SDK already creates one:

| Host | Scope boundary | Created by |
|---|---|---|
| ASP.NET request | per request | framework |
| mediator command | **none** — rides the ambient scope | `Mediator` is transient (`src/Mediator/MediatorServiceCollectionExtensions.cs:23`) |
| message consume | per event | `src/Messaging/Transport/EventProcessingPipeline.cs:125` |
| event saga | per execution, shared by steps | `src/Messaging/EventSaga/Services/EventSagaService.cs:21` |
| outbox dispatch | per batch | `src/Messaging/Reliability/Ef/OutboxDispatcher.cs:301` |
| hosted service / job | per job | the activator |

Because the mediator creates no scope, a whole command chain (nested `SendAsync` included) shares one scope, one `DbContext`, one map. That is precisely the span problem 3 needs. A *mediator-scoped* cache would be **narrower** than the `DbContext` and would fragment the map's own dedup; a *DbContext-scoped* cache is the same thing as a DI-scoped one in practice.

Scoped vs pooled context:

- **`AddDbContext` (scoped)** — new instance per scope, fresh map, collected at scope end.
- **`AddDbContextPool`** — the *instance* is reused across scopes, but EF resets the change tracker on return. Verified **(P10)**: scope B received the **same context instance** with **0 tracked entries**, and its `Find` hit the database. **Pooling does not leak the identity map.**
- The corollary is the argument against hanging a custom cache off the context: pooling resets only what EF knows about. A field you add is *not* reset, and it would survive into the next request on the same instance.
- **Two contexts in one scope = two maps**, no sharing, 2 round trips **(P12)**. Multi-`DbContext` apps get no cross-context dedup, and can hold two divergent instances of the same logical row without any exception.

Background jobs are not a special case — they get a scope, therefore a map. The special case is code that runs *outside* any scope (a singleton timer resolving from the root provider); such code has no read cache and must not be given one.

---

## Read-your-own-writes matrix

Rows marked **(P)** were verified; the rest follow from connection/transaction semantics.

| # | Writer | Reader | Sees |
|---|---|---|---|
| 1 | EF `Add` (unsaved) | EF `Find` | the new entity, 0 SQL **(P8)** |
| 2 | EF `Add` (unsaved) | EF LINQ query | **not** the new entity — the query runs against the DB, the row isn't there yet |
| 3 | EF mutation on a tracked entity | EF `Find` | the modified instance (current values) |
| 4 | EF `SaveChanges` | Dapper on the **same** connection + transaction | fresh (needs the enlistment question 2/3 are about) |
| 5 | EF `SaveChanges` | Dapper on a **different** connection | invisible until commit — today's default: `DataSourceConnectionFactory.CreateOpenAsync` opens a fresh connection per call |
| 6 | **Dapper write** | EF `Find` (tracked) | **stale** — the pre-write value, 0 SQL **(P9)** |
| 7 | **Dapper write** | EF LINQ **with tracking** | **stale** — the query runs, returns the fresh row, and EF discards it in favour of the already-tracked instance **(P9)** |
| 8 | Dapper write | EF `AsNoTracking` | fresh **(P9)** |
| 9 | Dapper write | `Entry(e).ReloadAsync()` | fresh **(P9)** |
| 10 | Dapper write | Dapper read, same connection/transaction | fresh |
| 11 | Dapper write | Dapper read, different connection | depends on commit |
| 12 | EF `ExecuteUpdate` / `ExecuteDelete` | EF `Find` (tracked) | **stale** — same shape as row 6; these bypass the change tracker by design |

**Row 7 is the answer to the question as posed.** *"What if the write went through Dapper and the read cache holds the pre-write value?"* — the cache wins **silently, even though the SQL was issued**. EF does not clobber the current values of a tracked entity with query results. The only escapes are `Reload()`, detach-then-requery, or `AsNoTracking`.

Restated as the design rule:

> Layer C sees layer B's write **iff B wrote through the same `DbContext`**. If B wrote out-of-band (Dapper command, `ExecuteUpdate`, raw SQL), C sees the stale value and **nothing signals it**.

---

## Memory and lifetime hazards

- **Snapshot cost.** A tracked entity holds a strong reference *plus* an original-values snapshot — roughly 2× the graph. `GetAllAsync` on a tracking read repository materializes and pins the whole table for the scope's life.
- **`DetectChanges` is the real cost, not memory.** It is O(tracked entities × properties) and runs on every `SaveChanges` and on most `ChangeTracker` API calls. A request that accumulates 10⁴ entities pays it repeatedly. "Just turn tracking on everywhere" is a CPU decision more than a RAM one.
- **Streaming / export endpoints.** Tracking converts a constant-memory `IAsyncEnumerable` stream into an O(rows) one. Such reads are read-once and never revalidated, so they gain nothing from the map — they must stay no-tracking.
- **Long-running requests** (batch import, migration job). The map grows monotonically. Mitigations are `ChangeTracker.Clear()` between units, or a nested scope per unit — both of which discard the read cache, which is correct: the cache's span becomes the unit, not the request.
- **Pooled contexts** hold no entities after reset **(P10)**, but the pool defaults to 1024 contexts; their internal query/model caches persist by design.
- **Entity shape matters** — owned types, JSON columns, and concurrency tokens (`IHasXmin`, `IRowVersioned`, `IVersioned`) all enlarge the snapshot.

Bounding rule the design should adopt: **the read cache is a dedup for the validate-then-write path, not a general cache.** Only by-key reads participate. List, report, and stream reads stay untracked.

---

## The tenant-leak hazard

**Mechanism (verified, P3):** a foreign-tenant row placed in the map is returned by `Find` with **0 SQL and no filter evaluation**, while the identical LINQ query on the same context correctly returns `null`. `ApplyTenantFilter` installs a *query* filter — it can only run when a query runs.

Two ways a foreign row reaches the map:

1. **Through Dapper.** `DapperRepository` emits no tenant predicate and no soft-delete predicate for any read (`src/Data/Dapper/Repositories/DapperRepository.cs:48,57,67`). Any Dapper-materialized row is un-filtered *by construction*, so the question-1 read→write handoff (Dapper reads it, EF attaches it) is a direct leak path.
2. **Through a store keyed on `(type, id)`** without the tenant, living longer than one scope.

Cross-request leakage of the **EF map itself is not a current risk** — scoped contexts are per scope, and pooled contexts are reset **(P10)**. *The risk is entirely in what a new store would add.* That asymmetry is the strongest argument in this document.

Structural mitigations, strongest first:

1. **Don't build a second store for entities.** The leak surface is currently closed; adding a store re-opens it. This is a structural mitigation, not a discipline one.
2. **If a non-entity store is built, make the tenant part of the key *type*, not of a key string.** A key that cannot be *constructed* without a tenant makes the leak impossible rather than unlikely:
   ```csharp
   // shape only — a non-nullable tenant segment inside the key type,
   // built by a factory that reads ITenantContext and throws when absent
   readonly record struct ReadKey(string TenantId, Type EntityType, object Id);
   ```
   Contrast `CacheKeyBuilder.Build(params string[])` (`src/Caching/Core/CacheKeyBuilder.cs:7`) — a `string[]` join, where omitting the tenant is a silent, reviewable-only mistake. A read cache must not repeat that shape.
3. **`AddScoped`, resolved from the scope's provider. Never a singleton, never a static, never `AsyncLocal`.** `AmbientTenantContext` is a singleton + `AsyncLocal` (`src/Tenancy/Core/AmbientTenantContext.cs:10`) — correct for a *scalar* set once per request, wrong for a *store of rows*: the ambient value flows into every `Task.Run` continuation and every un-suppressed `ExecutionContext` capture, so a detached background continuation inherits a live reference to another request's row set. `HttpContext.Items` is equally wrong — it disappears for the message/saga/outbox/job scopes above.
4. **Assert on the way *in* to the map, never on the way out.** Once a row is tracked, `Find` will hand it over. The cheap version: at the attach seam, if the entity is `IHasTenant<T>` and its `TenantId` differs from `ITenantContext.TenantId`, **throw** — do not filter, do not skip.
5. **Make the Dapper read path filter-aware** before it can feed EF: a tenant predicate for `IHasTenant<T>` entities and an `is_deleted` predicate for `ISoftDeletable`, or an explicit opt-out.

Soft delete has the identical shape and a lower severity: a Dapper-read soft-deleted row, once attached, is returned by `Find` even though the EF filter would exclude it.

---

## Staleness and the partial-entity interaction

One question-1 result matters directly to the cache **(P11)**: attaching a partially-populated entity (Dapper selected only `id` + `total`) and then mutating it marks **only the mutated property** as modified, and `SaveChanges` wrote only that column — the untouched `tenant_id` survived in the database.

Write-safe. **Read-unsafe.** The instance now sitting in the identity map has `TenantId = null`, and every later `Find` in that scope hands out that instance. A validation layer that reads `TenantId` from the cached entity reads `null` and cannot tell the difference between "no tenant" and "not selected".

> A partially-materialized entity is safe to *write through* and unsafe to *cache*. If the map is the read cache, partial attach must be either forbidden or marked, because the map has no concept of "this property was never loaded".

---

## What this forces on the pipeline design

1. **Scope unit = DI scope.** Everything already uses it: EF's map, the consumer, the saga runner, the outbox dispatcher. Not `HttpContext`, not a mediator-owned scope.
2. **The map is authoritative for entities.** Any layer that materializes an entity must attach it or hand it to whoever attaches. Two layers materializing the same row is an exception, not a miss **(P7)** — a stronger constraint than any cache imposes.
3. **A per-scope store is permitted only for non-entities**, and its key type must carry the tenant.
4. **Out-of-band writes must publish invalidations into the scope.** Dapper commands, `ExecuteUpdate`, `ExecuteDelete` — each must surface affected keys so the pipeline can `Reload` or detach, or rows 6/7/12 of the matrix bite silently. This is question 2's defer/compensate machinery one level down: **reuse that channel, do not invent a second one.**
5. **Tracking becomes a per-operation decision.** `NoTrackingByDefault` must stay `false` **(P4)**, and the pipeline needs an explicit "this read is a projection/stream — don't track" affordance rather than `AsNoTracking` sprinkled at call sites.
6. **`IReadRepository` needs a map-eligibility seam.** Only `GetByIdAsync` can be served from the map. The interface should distinguish key lookups from queries, otherwise the pipeline cannot know which reads are dedup-able.
7. **Rollback must clear the map, not just the cache.** After a rollback the tracked entities hold values the database never committed — problem 2's shape, expressed in the identity map. The scope usually ends immediately and hides it; a saga step or an outbox batch that continues after a failed unit does not.
8. **Declare one `DbContext` per scope, or make the cache explicitly context-scoped.** Two contexts silently hold two divergent instances of the same row **(P12)**.

---

## Cheapest path to closing problem 3

In dependency order, smallest first:

1. `EfRepository.GetByIdAsync` → `FindAsync`; `DeleteByIdAsync` inherits the benefit. Removes the duplicate read for every EF-repository consumer **(P1)**.
2. Route the previous-version read on the write path through the tracking EF read (or attach the Dapper result — question 1), so the second validation layer's `Find` is free **(P6)**.
3. Add the attach-seam tenant assert (mitigation 4) before either of the above ships, because both increase the number of externally-materialized rows entering the map.
4. Only then consider a per-scope memo, and only for non-entity read models.

---

## Appendix — probe results

`net10.0` · `Microsoft.EntityFrameworkCore.Sqlite 10.0.10` · `Dapper 2.1.66` · shared in-memory SQLite · entity with named query filters `"SoftDelete"` (`!IsDeleted`) and `"Tenant"` (`TenantId == current`, closing over a holder object exactly as `ApplyTenantFilter` does) · SQL counted via a `DbCommandInterceptor`. Seed: `(1,'acme',100,live)`, `(2,'globex',200,live)`, `(3,'acme',300,soft-deleted)`; current tenant `acme`.

| # | Probe | Result |
|---|---|---|
| P1 | `Find` twice | `sql=1` then `sql=1`; same reference; 1 tracked entry |
| P2 | `Find` on a soft-deleted row / a foreign-tenant row | both `null`, query executed — **filters applied on the DB path** |
| P3 | `Attach` foreign-tenant row, then `Find` | returns `tenant=globex` at `sql=0`; the same LINQ query returns `null` — **filters bypassed on the map path** |
| P4 | global `QueryTrackingBehavior.NoTracking` | `Find` #1 `sql=1`, #2 `sql=2`, 0 tracked, different instances |
| P5 | `AsNoTracking()` read, then `Find` | 0 tracked after the read; `Find` re-queries; different instances |
| P6 | Dapper read → `Attach` → `Find` → mutate → `SaveChanges` | `Find` `sql=0`, same reference, `Unchanged` → `Modified`; one `UPDATE`; DB value applied |
| P7 | second instance, same key | `Attach` **and** `Update` throw `InvalidOperationException` ("another instance with the same key value … is already being tracked") |
| P8 | `Find` an `Added` (unsaved) entity | returned from the tracker, `sql=0` |
| P9 | out-of-band `UPDATE`, then read | `Find` → stale · tracking LINQ → **stale despite executing** · `AsNoTracking` → fresh · `Entry.ReloadAsync()` → fresh |
| P10 | `AddDbContextPool`, two scopes | scope B gets the **same instance**, **0 tracked entries**, `Find` `sql=1` — map does not survive a lease |
| P11 | partial attach (`id`+`total` only), mutate, save | only `Total` modified; `UPDATE` touched one column; `tenant_id` intact in the DB but `null` on the cached instance |
| P12 | two contexts in one scope | `sql=2` — two maps, no sharing |
| P13 | `Local.FindEntry(key)` | `null` before load, entry after — **map-only lookup, no DB fallback** |
| P14 | `AsNoTrackingWithIdentityResolution()` twice | `sql=1` then `sql=2`, different instances, 0 context-tracked — identity map is per *query* |

---

## Cross-references

- **Question 1 — EF attach semantics** (partial vs full entity, concurrency tokens, shadow properties, owned types, navigations): sibling research doc in this folder. This document assumes attach is legal for the entity shape and only records the identity-map consequence (P11).
- **Question 2 — transaction-aware caching**: the invalidation channel required by point 4 above is the same channel. One mechanism, two consumers.
- **Question 3 — where the pipeline sits**: constrained by this document to the **DI scope**; a mediator-owned scope would fragment the map.
