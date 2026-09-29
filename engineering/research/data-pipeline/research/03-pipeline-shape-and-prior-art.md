# Pipeline shape & prior art — research

*Last updated: 2026-07-22 · Status: **RESEARCH ONLY** — no code written, no decision taken. Answers questions **3** (where the pipeline sits) and **5** (prior art) of [`data-pipeline-idea.md`](../data-pipeline-idea.md).*

> **How this was derived:** read the SDK's own data/caching/messaging/mediator code (`src/Data/**`, `src/Caching/**`, `src/Messaging/Transport/**`, `src/Mediator/**`); external = EF Core docs + `dotnet/efcore` source (`BatchExecutor`), Marten docs, Wolverine docs, Spring Framework javadoc, NHibernate docs, linq2db.EFCore repo, repodb.net, NuGet/GitHub API for maintenance status. Every version and date below was checked, not recalled. Sources in §5.

---

## 0. Verdict

**Recommended seam: a scoped `IDataSession` (unit of work) that owns the connection + transaction + an after-commit synchronization list — opened and committed by a mediator behavior, fed by EF interceptors, decorated at the repository.** Composition of seams 2+3+4, with the *center of gravity at 2*.

One-line why: the two problems the owner actually named — transaction scope and cache-survives-rollback — are **lifetime** problems, not **interception** problems, and a `next()` chain cannot solve a lifetime problem. A filter wrapping read #1 has already returned by the time write #7 rolls it back. The primitive that solves it is a **registration list** (`afterCommit` synchronizations), not a chain.

Second verdict, stated because it is worth more than the first: **§3 — this is ~85% reinvention.** Every component is mature prior art; only the *composition across EF + raw SQL + two-tier cache* has no off-the-shelf answer. That makes the real build 3 small components, not a pipeline framework.

---

## 1. Question A — where does the pipeline sit?

### 1.1 Seam comparison

| | 1 · repository decorator | 2 · unit of work / SaveChanges | 3 · mediator behavior | 4 · EF interceptors |
|---|---|---|---|---|
| **blast radius** | smallest — opt-in per `AddCqrsRepository` registration | largest — redefines what `IWriteRepository` *means* | only mediator-dispatched work | global per `DbContext`, zero caller change |
| **sees** | entity type · id · CRUD verb · instance | every tracked entry · transaction lifetime · commit/rollback outcome | request + response type only | `SaveChanges`: full `ChangeTracker` · `DbCommand`: every SQL EF issues · `DbTransaction`: begin/commit/rollback/savepoint |
| **cannot see** | hand-written SQL · direct `DbSet` LINQ · `ExecuteUpdate/Delete` · sibling repos' work | nothing inside the session; blind to work outside it | entities, SQL, anything at all below the handler | anything Dapper does · the business intent behind the SQL |
| **enlist in txn** | **no** — has no transaction to enlist in | **yes** — the only seam that can own the after-commit list | begin/commit **yes**, per-write enlist **no** | **yes** via `IDbTransactionInterceptor` — with a hole (§1.2.4) |
| **Dapper read passes through** | yes via `IReadRepository`; **no** for hand-written SQL | only if handed the session's `DbConnection`+`DbTransaction` — impossible today (§1.4) | transitively only | **never** |
| **testability** | best — pure interface decoration, no DB | medium — needs a real connection or a session fake; the after-commit list itself is trivial | best — `IPipelineBehavior` chain already exercised in `Mediator.Tests` | medium — needs a `DbContext`; `Testing.Data` / `RelationalTestDb` already covers this |
| **forces on caller** | nothing | must not call `SaveChanges` itself; must open/close a scope | everything must route through the mediator | nothing |
| **fit** | read-cache lookup + key derivation | **transaction + after-commit + identity map** | begin/commit boundary | stamping, enlistment source-of-truth, SQL-level cache |

### 1.2 Per-seam detail

#### 1.2.1 Repository decorator
- decorating `IReadRepository`/`IWriteRepository` is cheap and reversible — `AddCqrsRepository<TEntity,TId,TContext>` is already the single registration point (`src/Data/CqrsRepositoryServiceCollectionExtensions.cs:25`).
- it is blind by construction. `DapperRepository`'s own doc says complex queries are hand-written SQL — every one of those bypasses the decorator. So does any `context.Set<T>().Where(...)` a handler writes directly.
- **fatal for transactions**: `EfRepository` calls `SaveChangesAsync` inside *every* write (`src/Data/EntityFrameworkCore/Repositories/EfRepository.cs:50,59,67,75,86`), and `IWriteRepository`'s contract documents it (`src/Data/Abstractions/IWriteRepository.cs:4` — "Implementations persist immediately"). A decorator cannot group two writes into one transaction without breaking that documented contract.
- good for: per-entity read caching, cache-key derivation from `TEntity`+`TId`, request-scoped read memoization (idea-doc problem 3).

#### 1.2.2 Unit of work / SaveChanges
- the only seam where "an inner layer enlists a side effect in a transaction it does not own" is expressible at all. Everything else has to delegate here.
- changing `IWriteRepository` from persist-immediately to stage-and-flush is a **breaking semantic change** to a published beta API, and it changes error timing: a constraint violation moves from the `CreateAsync` call site to the commit call site. That relocation is the real cost, not the code.
- prior art is unanimous that the caller must be *prevented* from committing, not merely asked: Wolverine documents "it's best to not directly call `IDocumentSession.SaveChangesAsync()` yourself", and Marten enforces it structurally by handing handlers `IDocumentOperations` — the session **minus** the commit method. That trick transfers directly here (hand handlers a write surface with no `SaveChanges`).
- Dapper only participates if the session hands it the live `DbConnection` + `DbTransaction`. See §1.4 for why that is blocked today.

#### 1.2.3 Mediator behavior
- the chain already exists and ordering already matters — 5 behaviors ship (`Validation`, `Authorization`, `Idempotency`, `Logging`, `ExceptionToResult`), folded registration-order → outermost in `Mediator.DispatchTyped` (`src/Mediator/Mediator.cs:68-79`).
- right place to *open and commit*; useless for *enlistment* — it never sees an entity or a SQL string.
- its blast radius is a coverage hole, not a safety property: hosted services, Hangfire jobs, message consumers (`EventProcessingPipeline` creates its own scope), and any controller touching a repository directly all bypass it silently. A transaction boundary that silently does not exist on half the call paths is worse than none.
- consequence: the mediator behavior must be a *convenience wrapper over* the session, never the owner of it. Same relationship Wolverine's transactional middleware has to Marten's `IDocumentSession`.

#### 1.2.4 EF interceptors
- the SDK already runs 3 `SaveChangesInterceptor`s in production — `AuditInterceptor`, `SoftDeleteInterceptor`, `TenantStampInterceptor` — auto-wired into every registered context (`src/Data/EntityFrameworkCore/EntityFrameworkCoreServiceCollectionExtensions.cs:66-68` + `AddEfInterceptor<T>`). The seam is proven here, not hypothetical.
- interceptors are **not** mere observers — EF docs: interceptors "enable interception, modification, and/or suppression of EF Core operations", and the docs ship a second-level-cache sample that suppresses a query via `InterceptionResult<DbDataReader>.SuppressWithResult(...)` — with an explicit warning that it is *not* a template for a robust second-level cache.
- `IDbTransactionInterceptor` gives `TransactionCommitted` / `TransactionRolledBack` — the closest thing EF has to Spring's `afterCommit`. It fires for EF's implicit `SaveChanges` transaction too, because `BatchExecutor` creates it through `connection.BeginTransaction()`, which routes via `RelationalConnection` into the interceptor.
- ⚑ **the trap**: under the default `AutoTransactionBehavior.WhenNeeded`, `BatchExecutor` *skips the transaction entirely* for a single batch that doesn't require one — verified in source: the begin is gated on `(WhenNeeded && (batch.AreMoreBatchesExpected || batch.RequiresTransaction)) || Always`. So a commit hook built only on `IDbTransactionInterceptor` **silently never fires for the single-entity save**, the most common case. Mitigation: own the transaction explicitly at the session, or force `AutoTransactionBehavior.Always`.
- ⚑ second trap: `ISaveChangesInterceptor.SavedChangesAsync` is **not** after-commit. When the app owns an outer explicit transaction, `SavedChanges` fires when `SaveChanges` returns — before `CommitAsync()`. A cache write deferred to `SavedChanges` still survives an outer rollback. This is exactly the bug in idea-doc problem 2, just moved one frame.
- ⚑ third trap: `ExecuteUpdate` / `ExecuteDelete` bypass the change tracker and `SaveChanges` entirely, so every SaveChanges-based seam misses them.
- blind to Dapper, permanently. EF interceptors see only `DbCommand`s EF itself issues.

### 1.3 Does the `IConsumeFilter` shape transfer?

**Partially — and not for the part the owner cares about.** The analogy is doing less work than it appears to.

Where it transfers:
- per-operation cross-cutting concerns with a single call boundary — read-cache lookup/short-circuit, metrics, logging, tenant scoping, retry-on-transient. These are shaped exactly like `IConsumeFilter`: wrap a core, optionally short-circuit, empty-by-default. `BuildChain()` (`src/Messaging/Transport/EventProcessingPipeline.cs:93-103`) is directly copyable for a per-repository-call chain.

Where it does **not** transfer, and why data access is different in kind:

| | message consumption | data access |
|---|---|---|
| unit of work | **1 call = 1 unit.** `ProcessAsync(ReceiveContext)` *is* the whole unit | **N calls = 1 unit.** many reads + many writes, commit boundary lives elsewhere |
| ambient state | `ReceiveContext` exists **before** the chain runs, passed to every filter | the transaction is created **lazily, mid-flight, by EF** — and sometimes not at all (§1.2.4) |
| outcome visibility | filter sees the whole outcome, it wraps the core | filter for read #1 has **returned** before write #7 decides the outcome |
| retry | the core is re-runnable; resilience retries it wholesale | a partially-flushed write is not re-runnable |
| short-circuit | dropping a message is safe and meaningful | short-circuiting a *read* = cache hit (fine); short-circuiting a *write* is not the same act |
| bypass | no other client talks to the broker behind the pipeline's back | **Dapper always does** — a raw `DbConnection`, outside every EF seam |

The decisive one is row 3. The core problem — "an inner layer's cache entry must not survive an outer layer's rollback" — requires a hook that fires **after** the inner filter's stack frame is gone. A chain has no such hook. Every mature system solves it with the *other* shape:

- Spring → `TransactionSynchronizationManager.registerSynchronization(...)` + `afterCommit`
- Marten → `IDocumentSessionListener.AfterCommitAsync`
- Hibernate/NHibernate → cache concurrency strategy with soft locks, real write after commit
- .NET BCL → `Transaction.Current.TransactionCompleted` / `IEnlistmentNotification`

All four are **registration lists on a transaction**, not filter chains. Build that first; add the chain second, if it still earns its place.

### 1.4 Concrete blockers in today's code

- `DataSourceConnectionFactory.Create()` / `CreateOpenAsync()` return a **new** connection from the `DbDataSource` every call (`src/Data/Abstractions/DataSourceConnectionFactory.cs:11-14`), and `DapperRepository` opens one per operation and disposes it (`src/Data/Dapper/Repositories/DapperRepository.cs:49,58,68,77,86,100,110,126`). The idea doc says EF and Dapper "share a connection" — **they share a pool, not a connection.** Today a Dapper read can never be inside an EF transaction. Any design that wants it to must introduce a session-aware `IDbConnectionFactory` that returns the session's connection when one is open.
- `EfRepository` commits per write (§1.2.1) — there is no unit of work in the SDK at all today. `grep` finds no `UnitOfWork`, no `IDbContextTransaction` usage outside migrations.
- `ICache.GetOrCreateAsync` (`src/Caching/Core/ICache.cs:21`) is a `putIfAbsent`-shaped API. Spring documents that `putIfAbsent`/`evictIfPresent` **cannot** be deferred to after-commit — the value must be produced and stored now. So the SDK's primary cache entry point is the one operation the blueprint says is undeferrable. Any transaction-aware decorator must either bypass the L2 write inside a transaction or return an uncached value. This is a real design constraint, not a detail.
- `Caching` is marked deferred in P3 (`CLAUDE.md` phase table) while `HybridCacheAdapter` already ships — confirm which is current before designing on it.

### 1.5 Recommended composition

| concern | seam | why |
|---|---|---|
| transaction lifetime, after-commit list, identity map | **2** scoped `IDataSession` | only seam that can outlive an inner call |
| begin/commit for HTTP command paths | **3** mediator behavior, thin wrapper over 2 | boundary already exists; must not be the owner |
| audit / soft-delete / tenant stamping | **4** already there | keep |
| enlistment truth (did it *actually* commit?) | **4** `IDbTransactionInterceptor` → notify session | with `AutoTransactionBehavior.Always` to close the hole |
| Dapper enlistment | session-aware `IDbConnectionFactory` | reference implementation: linq2db.EFCore (§2) |
| per-call read cache, metrics, key derivation | **1** decorator, optionally a `next()` chain | the only place the `IConsumeFilter` shape genuinely fits |

---

## 2. Question B — prior art

### 2.1 Table

| Tool | Version checked | Solves | Does **not** solve | Verdict |
|---|---|---|---|---|
| **Marten** | 9.17.1 | session-as-UoW; 4 session flavors (`QuerySession` · `LightweightSession` · `IdentitySession` · `DirtyTrackedSession`); identity map per flavor; `SessionOptions.ForTransaction/ForConnection/ForCurrentTransaction` enlists in an existing Npgsql tx or ambient `TransactionScope`; `IDocumentSessionListener` = `BeforeSaveChangesAsync` · **`AfterCommitAsync`** · `DocumentLoaded` · `DocumentAddedForStorage` | it is a **Postgres document store** (JSONB) — no help for an EF-mapped relational schema; no second-level cache; identity map is per-session, not per-request-across-engines | **adapt, don't adopt** — steal session flavors + `AfterCommitAsync` + commit-less write surface |
| **Wolverine** (transactional middleware) | MIT, JasperFx open-core | the mediator-seam answer in production: middleware opens the session, calls `SaveChangesAsync`, flushes the outbox after commit; hands handlers `IDocumentOperations` so they *can't* commit | relational/EF caching; same Postgres+Marten coupling | **adapt** — validates seam 3 as boundary, seam 2 as owner |
| **NHibernate / Hibernate** | — | session + flush modes + `IInterceptor`; 1L identity map; **2L cache concurrency strategies** — `read-only`, `nonstrict-read-write`, `read-write` (soft locks, real write **after** commit), `transactional`. The `read-write` strategy *is* the mature answer to cache-survives-rollback | in .NET no provider implements the `transactional` strategy; 2L cache guarantees only read-committed; whole-ORM adoption is a non-starter | **adapt the taxonomy** (it is the vocabulary for the cache design), **avoid the library** |
| **EF Core itself** | 9/10 | change tracker **is** the identity map; `AsNoTracking`; DbContext pooling; compiled queries; interceptors that can suppress + substitute results; `AutoTransactionBehavior`; `ExecutionStrategy` | no 2L cache; no reliable after-commit hook (§1.2.4 traps); zero visibility into Dapper; `ExecuteUpdate/Delete` bypass | **adopt as substrate** — already is |
| **EFCoreSecondLevelCacheInterceptor** | 5.5.0, maintained | the .NET EF 2L cache: `IDbCommandInterceptor`, keys on generated SQL, invalidates by touched tables on `SaveChanges` | **it refuses the problem** — queries inside an explicit transaction are *not cached by default* (`AllowCachingWithExplicitTransactions(true)` to opt in); `ExecuteUpdate/Delete` not invalidated | **read it, don't take it** — the closest .NET answer solves rollback-coherence by *avoidance*. Strongest evidence the hard half is uncommoditized |
| **Dapper.Contrib** | 2.0.78, published **2020-11-18**; repo last pushed 2024-05 | attribute-driven CRUD + optional interface dirty-tracking | no UoW, no identity map, no cache, no transaction ownership; **effectively dormant** | **avoid** |
| **Dapper-Extensions** | repo last pushed 2024-02 | CRUD + predicate system, POCOs stay clean | same gaps; dormant | **avoid** |
| **Dapper→EF bridge (the normal recipe)** | n/a | `ctx.Database.GetDbConnection()` + `ctx.Database.CurrentTransaction.GetDbTransaction()` handed to every Dapper call — the whole ecosystem does exactly this, manually | nothing automatic; every call site must remember | **adopt the recipe, hide it behind the session-aware factory** |
| **linq2db.EntityFrameworkCore** | 10.4.0, MIT | the mature solution to *"a second query engine sharing EF's connection and current transaction"*: `CreateLinqToDBContext` checks `context.Database.CurrentTransaction`; `ToLinqToDB()`; `CreateLinqToDBContextForScope` for `TransactionScope` | caching; UoW semantics beyond EF's | **adapt** — reference implementation for the Dapper-side enlistment. Consider **adopt** if the fast read path would rather be typed LINQ than raw SQL |
| **ServiceStack.OrmLite** | — | POCO micro-ORM, explicit `IDbTransaction`, fast | no change tracker, no identity map, no UoW, no transaction-aware cache. **AGPL + commercial with free quotas** | **avoid** — collides with the repo rule "permissive licenses only in core / no commercial-license dependencies in the core meta-package" |
| **RepoDB** | **1.15.0, published 2026-07-18**; real repo `mikependon/RepoDB` (pushed 4 days ago) — `orm-core-group/RepoDb` is a 0-star fork | explicitly targets the Dapper-speed/EF-convenience gap; **built-in 2nd-layer cache keyed per query** (`cacheKey:` + `ICache` + `cacheItemExpiration`, 180-min default); `ITrace` before/after hooks; property handlers | cache is **not transaction-aware** — docs do not address defer-to-commit and no such mechanism exists; no change tracker/UoW; transactions passed explicitly per call | **avoid as a dep, keep as evidence** — the closest .NET package to the ask, and it still leaves the hard half unsolved |
| **Spring `@Transactional` + `TransactionSynchronizationManager`** | Framework 6/7 | `registerSynchronization` + **`afterCommit`** — the canonical enlistment seam. `TransactionAwareCacheManagerProxy` / `TransactionAwareCacheDecorator` defer `put`/`evict`/`clear` to after-commit; no active tx → immediate. Documented limitation: `putIfAbsent`/`evictIfPresent` **cannot** be deferred | it's Java; no dual-engine (EF+raw-SQL) story | **adapt — this is the blueprint for problem 2**, limitation included (§1.4) |
| **jOOQ** | — | `ExecuteListener` / `TransactionListener` / `RecordListener` / `VisitListener` — **separate listener types per lifecycle stage** rather than one god-filter; typed SQL DSL | caching, identity map (deliberately — not an ORM); dual-licensed (commercial for Oracle/SQL Server) | **adapt the listener taxonomy**, avoid otherwise |
| **.NET `System.Transactions`** | BCL | `Transaction.Current`, `IEnlistmentNotification` (2PC: `Prepare`/`Commit`/`Rollback`), `TransactionCompleted` event — the BCL's built-in after-commit. Npgsql enlists; EF's `BatchExecutor` already checks `CurrentAmbientTransaction` | promotion/distributed semantics are a heavy hammer for one connection; ambient state is hard to reason about across async | **borrow the vocabulary** (`Prepare`/`Commit`/`Rollback` per enlistment), **probably avoid the ambient path** |
| **MassTransit / GreenPipes** | GreenPipes stalled at 4.0.1; MassTransit 9.x is commercially licensed | the ancestor of this repo's `IConsumeFilter` shape | nothing at the data layer | **note only** — the standalone generic pipe library was retired back in-tree; generalized pipelines age badly once payload types diverge |

### 2.2 The two entries that matter most

**Spring's `TransactionAwareCacheDecorator`** — idea-doc problem 2 verbatim: defer `put`/`evict`/`clear` to the after-commit phase, act immediately when no transaction is active, and the inner layer keeps its keys private because the decorator wraps the *cache*, not the caller. It is ~15 years old, ~200 lines, and its documented limitation (`putIfAbsent` undeferrable) is precisely the constraint the SDK's `ICache.GetOrCreateAsync` will hit. There is nothing to invent in the mechanism — only to port.

**Marten's session flavors** — the answer to idea-doc problem 3 (duplicate reads across validation layers) that nobody usually notices: `IdentitySession` makes the identity map a *per-session opt-in*, not a global policy, so the "read it once, reuse it in every later layer" behaviour has a switch and a cost the caller chose. The alternative shapes (make Dapper populate EF's change tracker; a separate per-request store) are question 4's problem, but Marten's answer is the design precedent: **flavor the session, don't flavor the repository**.

---

## 3. The novelty verdict

**Reinvention, ~85%. One genuinely unsolved seam, and it is narrower than the idea doc implies.**

Component by component:

| Claimed novelty | Reality |
|---|---|
| ordered pipeline over data read/write | **not novel** — jOOQ listener SPI, NHibernate `IInterceptor`, EF interceptors, RepoDB `ITrace`, Marten `IDocumentSessionListener` all ship it |
| unit of work + identity map + session flavors | **not novel, and today's SDK is behind** — Marten, NHibernate, EF all have it; EF's change tracker *is* an identity map. This is catch-up work, not vector work |
| transaction-aware caching | **not novel** — Spring's `TransactionAwareCacheDecorator` and Hibernate's `read-write` region strategy solved it over a decade ago. It **is** novel *in .NET/EF Core*: nothing mainstream ships it, and the one serious .NET attempt (`EFCoreSecondLevelCacheInterceptor`) opts out of caching inside explicit transactions rather than defer |
| Dapper read → EF write sharing one connection + transaction | **solved** — generically by linq2db.EntityFrameworkCore, manually by the `GetDbConnection()`/`CurrentTransaction` recipe every Dapper+EF codebase already uses |
| layer-owned cache keys invisible to outer layers | **not novel** — that is just decoration; Spring gets it for free the same way |

**What has no off-the-shelf answer, precisely stated:** one session that spans *both* an EF write path and a raw-SQL read path *and* a two-tier (L1+L2) cache, where an inner layer's cache write enlists in a transaction it does not own and is compensated on rollback without the outer layer knowing its keys. Every prior art covers a strict subset — Spring has the cache half without the dual-engine half; linq2db has the dual-engine half without the cache half; Marten has both but only inside its own document store; RepoDB has a cache and no transaction-awareness.

**So the novel part is the composition, not any component.** That reframes the build:

- ~~an ordered, layered pipeline over data access~~ → **three small components**
  1. scoped `IDataSession` — owns connection + transaction + `RegisterAfterCommit(...)` list
  2. transaction-aware `ICache` decorator — port of `TransactionAwareCacheDecorator`, minus `GetOrCreateAsync` inside a transaction
  3. session-aware `IDbConnectionFactory` — so Dapper enlists (crib linq2db.EFCore)
- the `IConsumeFilter`-shaped chain is **optional and last**, not the frame.

If the owner would rather not build even that: **Marten + Wolverine already does 80% of it end-to-end, MIT-licensed, Postgres-only** — which is the SDK's target database anyway. The blocker is not licensing or maturity, it is that Marten is a document store and this SDK is committed to an EF-mapped relational schema. That is a real blocker, but it is the *only* one, and it should be named explicitly before building rather than discovered after.

---

## 4. Carried into the design pass

- decide the `AutoTransactionBehavior` policy **before** anything depends on `TransactionCommitted` (§1.2.4).
- `ICache.GetOrCreateAsync` inside a transaction needs an explicit, documented answer — bypass L2, or return uncached (§1.4).
- session-aware `IDbConnectionFactory` is a prerequisite for *everything* involving Dapper enlistment; nothing else can start until it exists.
- `IWriteRepository`'s "persists immediately" contract (`src/Data/Abstractions/IWriteRepository.cs:4`) is load-bearing and published — changing it is a breaking change with relocated error timing.
- open question left to research #1 and #4: EF partial-attach semantics and where the request-scoped read cache lives. This report assumes neither.

---

## 5. Sources

- EF Core interceptors — https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors
- EF Core `BatchExecutor` (implicit transaction gating) — https://raw.githubusercontent.com/dotnet/efcore/main/src/EFCore.Relational/Update/Internal/BatchExecutor.cs
- Marten — sessions & identity map — https://martendb.io/documents/sessions · listeners — https://martendb.io/diagnostics
- Wolverine — Marten transactional middleware — https://wolverinefx.net/guide/durability/marten/transactional-middleware · outbox — https://wolverinefx.net/guide/durability/marten/outbox.html
- JasperFx licensing plans (Marten 8 / Wolverine 4 stay MIT) — https://jasperfx.net/news/jasperfx-in-2025
- Spring `TransactionAwareCacheManagerProxy` — https://docs.spring.io/spring-framework/docs/current/javadoc-api/org/springframework/cache/transaction/TransactionAwareCacheManagerProxy.html · `TransactionAwareCacheDecorator` — https://docs.spring.io/spring-framework/docs/current/javadoc-api/org/springframework/cache/transaction/TransactionAwareCacheDecorator.html
- NHibernate caches / concurrency strategies — https://nhibernate.info/doc/nhibernate-reference/caches.html · https://nhibernate.info/blog/2009/04/24/nhibernate-2nd-level-cache.html
- linq2db.EntityFrameworkCore — https://github.com/linq2db/linq2db.EntityFrameworkCore
- RepoDB caching — https://repodb.net/feature/caching · repo — https://github.com/mikependon/RepoDB
- EFCoreSecondLevelCacheInterceptor — https://github.com/VahidN/EFCoreSecondLevelCacheInterceptor
- ServiceStack licensing — https://github.com/ServiceStack/ServiceStack/blob/master/license.txt
- Dapper.Contrib — https://github.com/DapperLib/Dapper.Contrib · Dapper-Extensions — https://github.com/tmsmith/Dapper-Extensions
