# Data pipeline — research synthesis + verdict

*2026-07-19. Synthesized from 5 independent research agents. Inputs: [`research/01`](./research/01-ef-attach-semantics.md) … [`research/05`](./research/05-failure-modes.md). Idea as captured: [`data-pipeline-idea.md`](./data-pipeline-idea.md).*

## Verdict

**Don't build the pipeline. Fix the data layer first — the research found live defects underneath it — then build three small components, not a chain.**

Three independent findings converge on this:

- the pipeline's **enabling premise is false today** (EF and Dapper cannot share a transaction — see D1)
- the filter-chain shape **does not transfer** from the events layer: transaction scope is a *lifetime* problem, and a `next()` chain has already returned by the time an outer layer rolls back. Every mature system uses a **registration list on a transaction**, not a chain — Spring `afterCommit`, Marten `AfterCommitAsync`, Hibernate soft locks, BCL `IEnlistmentNotification`
- **~85% of the vector is mature prior art**. Only the composition — one session spanning EF + raw SQL + a two-tier cache with layer-private keys — is uncommoditized

---

## Defects found in the existing data layer

Found while researching, all in shipped code, none related to the pipeline. **These are worth fixing whether or not the vector is built.**

| # | Defect | Impact |
|---|---|---|
| D1 | **EF and Dapper share a connection *pool*, not a connection.** `DataSourceConnectionFactory.Create()` mints a new connection; `DapperRepository` opens/disposes one per call (8 sites) | A Dapper write inside an EF transaction **commits independently** — silently, no log. The idea doc's premise, and a live correctness bug |
| D2 | **`DapperRepository` emits no tenant and no soft-delete predicate** on any of its 4 reads | Un-filtered by construction. `IHasTenant` exists; this path ignores it. **Cross-tenant read** |
| D3 | **`SELECT *` never returns `xmin`** (Postgres system column), and `DapperRepository` uses `SELECT *` | Every `IHasXmin` entity read that way arrives `Xmin = 0` → `DbUpdateConcurrencyException` **100% of the time**. Verified with psql |
| D4 | **`EfRepository.UpdateAsync` calls `Set.Update(entity)`** | Flags every non-key property regardless of what was loaded. A partially-populated entity writes `NULL`/`0` over real columns, in one statement, no error |
| D5 | **`AddPostgresPersistence` never runs the interceptor auto-wire loop** | Anything registered via `AddEfInterceptor` is **silently dropped** under the flagship registration |
| D6 | **No unit of work** — `EfRepository` calls `SaveChangesAsync` inside every write | There is no rollback boundary to defer anything to. Precondition for everything else here |
| D8 | **`EntityFrameworkCoreOptions` is a record with `init`-only properties**, configured through an `Action<EntityFrameworkCoreOptions>` — the callback can never assign anything | `UsePooling` is stuck at `true`; non-pooled registration is **unreachable**. Found 2026-07-24 |
| D9 | **`Testing.Data.RepointDbContext` re-adds a bare `AddDbContext`** (`DbContextProviderSwapExtensions.cs:45`) | Test hosts silently lose **every** interceptor, audit included. Same family as D5; one line (`.AddRegisteredInterceptors(sp)`) |
| D7 | **`AddTenantRowStamping()` is never invoked** under `AddPostgresPersistence` — it plugs in via `AddEfSaveChangesInterceptor`, which the flagship registration never enumerates (same root cause as D5) | Registered, resolvable, **never runs**. Rows insert with a null tenant while its XML doc promises the opposite. 5 services call `AddPostgresPersistence`; none has tenancy on yet, which is the only reason it isn't burning |

D2 is a security bug. D1, D3, D5 are silent-failure bugs of the same class this session has been finding all day in the events layer.

---

## What the research settled

### Q1 — EF attach: **conditional, and narrower than hoped**

- `Attach` + mutate, or `IsModified`/`CurrentValue`, emits a narrow `UPDATE` and issues **0 SQL** on attach — this works
- `Update()`, `Entry.State = Modified`, and `OriginalValues.SetValues` on a partial entity are all **fatal** (D4)
- `Attach` snapshots what the object holds, so EF **cannot distinguish** "Dapper never selected it" from "the caller cleared it". That ambiguity is the whole hazard
- **absent is safe, half-present is dangerous** — a null owned reference preserves its columns; one built with defaults writes `0`
- audit/soft-delete/version interceptors still fire on an attached entity, **but only with `AutoDetectChangesEnabled = true`**
- best fit for the owner's problem 3: cache the **full** previous row → `Attach` → `CurrentValues.SetValues(incoming)`. Only genuine diffs are written, so the validation cache and the minimal `UPDATE` pay for each other

### Q2 — transaction-aware caching: **achievable, with an honest ceiling**

- mechanism: transaction-scoped, **savepoint-aware invalidation-intent buffer**, drained post-commit, every action idempotent
- **eviction only, never update** — the cache-aside stale-set race is unfixable by transaction-awareness, and a safe update needs CAS, which `ICache`/`HybridCache`/`IDistributedCache` cannot express
- guarantee: converts *"a value the DB never committed"* (an isolation violation) into *"a value the DB committed a moment ago"* (ordinary staleness). That is the real, and sufficient, win
- **ceiling, stated honestly**: no XA between Postgres and Redis. Cross-node L1 is bounded only by `LocalCacheExpiration` — MS docs, verbatim: *"the in-memory cache in other servers isn't affected"*
- **`SavedChanges` is a safe collect point, not a safe apply point** — inside an explicit transaction it fires *before* commit, reproducing the exact bug. `IDbTransactionInterceptor` is the correct apply point
- **EF auto-creates a savepoint** before every `SaveChanges` inside a transaction and rolls back to it on failure. So "inner rolls back, outer commits" is EF's **default**, not a hypothetical — a flat buffer is wrong, intents must be depth-tagged

### Q3 — pipeline shape: **not a chain**

Recommended seam: a scoped `IDataSession` (unit of work) owning connection + transaction + an after-commit list; a mediator behavior opens/commits it; EF interceptors feed it; a repository decorator handles read caching.

Verified trap: under the default `AutoTransactionBehavior.WhenNeeded`, EF's `BatchExecutor` **skips the implicit transaction for a single-command save**, so `TransactionCommitted` never fires for the most common case.

### Q4 — request-scoped read cache: **use EF's identity map, build nothing**

- a second store isn't merely redundant — EF **throws** when a second instance with the same key is attached, so two stores that can each hand out an instance for one key is a forbidden state
- **the biggest win is one line**: `EfRepository.GetByIdAsync` uses `FirstOrDefaultAsync`, which never consults the map. `FindAsync` makes the second validation layer's read cost **0 round trips**
- verified tenant leak: a foreign-tenant row already in the map is returned by `Find` at `sql=0`, while the identical LINQ query returns `null`. `ApplyTenantFilter` is a *query* filter — no query, no filter
- 14 probes run against EF Core 10.0.10; `Find`'s DB path **does** apply global query filters (contradicting the common belief), its map path bypasses them entirely

### Q5 — failure modes: **build narrower**

- 9 of 16 enumerated failure modes are in the **"detectable? No"** column
- **`EnableRetryOnFailure` + explicit transaction throws**; the standard fix replays the whole delegate, so every layer's cache writes, outbox rows, `Version++` and audit stamps run **twice**. `VECTOR-ANALYSIS.md` already schedules adding retry — the collision is planned, not hypothetical
- the debuggability trade **inverts** versus the events layer: there, a chain recovered an already-lost async trace; here, it degrades a fully-present synchronous one
- the performance premise is **unfalsifiable today** — no `Data.Tests` project, no BenchmarkDotNet anywhere in the repo

---

## Sequence

Superseded by the arch doc's build order → [`data-session-architecture.md`](./data-session-architecture.md) § *Build order*. **Not recommended**: the ordered filter chain — Q3 shows why.

---

## The alternative worth naming

**Marten + Wolverine already does ~80% of this end-to-end** — MIT, Postgres-only, this SDK's target DB. Blocker: Marten is a document store; this SDK is committed to an EF-mapped relational schema.

Real blocker, and the *only* one. Deserves an explicit decision rather than late discovery.

---

## Open questions, with the test that settles each

| # | Question | Why it matters |
|---|---|---|
| 1 | HybridCache tag invalidation cross-node via Redis pub/sub? Docs say no, `dotnet/extensions#7098` says yes | decides tag- vs key-based drain |
| 2 | latency: attach-vs-query, savepoint cost, HybridCache deserialization | the attach win is premised on the round trip dominating — unverified |
| 3 | do transaction interceptors fire for EF's implicit transaction? | hook drain coverage |
| 4 | is the Npgsql auto-savepoint ever skipped? | session depth counter + hook frames |
