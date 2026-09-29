# 02 — Transactional cache coherence

*Last updated: 2026-07-22 · Status: **research — findings only**. No design commitment, no code written.*

> Research question 2 of [`../data-pipeline-idea.md`](../data-pipeline-idea.md) §2 — *"cache coherence breaks under rollback"*, the owner's core problem. Grounded in `src/Caching/` + `src/Data/` + the in-repo `src/Messaging/Reliability/Ef/` outbox, then against external prior art (Hibernate/NHibernate, EFCoreSecondLevelCacheInterceptor, Marten, MassTransit, MS docs for HybridCache + EF Core transactions/interceptors).
>
> **Framing correction up front:** the question as posed ("how do we make cache writes transaction-aware?") has a cheaper answer than it implies — *stop writing values into the shared cache from inside a transaction at all.* Everything below follows from that.

---

## 0. Verdict

**Mechanism: a transaction-scoped, savepoint-aware intent buffer that records only *invalidations*, drained after commit, with every action idempotent.** Concretely: defer-to-commit **and** compensate-on-rollback, where "compensate" degenerates to *discard the buffer* because nothing was ever applied; plus an optional pre-commit eviction as a race-narrowing leg (§4).

**The guarantee it actually provides** — stated as three separate claims, because they have different strengths:

| # | Claim | Strength |
|---|---|---|
| **G1** | For any key `k` dirtied inside transaction `T`, no cache tier ever holds a value produced by `T` before `T` commits. If `T` rolls back (fully or to a savepoint), the shared cache is byte-identical to a world where `T` never ran. | **Absolute** — holds under process death, because the mechanism never writes. |
| **G2** | If `T` commits, an eviction of `k` is attempted **at-least-once** at some point after the commit returns. Duplicate evictions are harmless (an eviction is idempotent; the cost is one cache miss). | **At-least-once, modulo G3** |
| **G3** | Between `T`'s commit and the eviction landing on a given node, readers on that node may observe the **pre-`T`** value. That window is: unbounded-until-TTL if the process dies in the gap (unless invalidations are staged durably, §6); and ≥ `LocalCacheExpiration` on *every other node* regardless of what `T` does (§7). | **Bounded staleness, not coherence** |

**Why this is the right trade, in one line:** it converts the failure mode from *"the cache holds a value the database never committed"* (an isolation violation — a value that never existed, readable by everyone) into *"the cache holds a value the database committed a moment ago"* (ordinary staleness — the contract every cache already operates under). The first is a correctness bug; the second is a tuning parameter.

**What it is not:** not linearizable, not read-through coherent, not exactly-once invalidation, and not a substitute for a TTL. TTL remains the backstop for every leg that can fail silently.

---

## 1. What is impossible — the ceilings

Name these before designing, because three of the four are commonly designed around by accident.

1. **Atomic {DB commit, Redis mutation} is impossible.** There is no 2PC path: Redis has no XA/JTA participation, and `System.Transactions` distributed transactions on .NET are **Windows-only, .NET 7+** ([EF docs, *Limitations of System.Transactions*](https://learn.microsoft.com/en-us/ef/core/saving/transactions)). Hibernate's `TRANSACTIONAL` strategy — the only one of its four that offers real coherence — explicitly *"requires a fully transactional cache provider… the cache and the database must cooperate via JTA or the XA protocol"*. **That strategy is unavailable to this stack.** Everything else is a choice of which side to be wrong on.
2. **Therefore: exactly-once cache invalidation is impossible.** The only choice is at-most-once (apply before commit — may invalidate for a transaction that never committed) or at-least-once (apply after commit — may lose the invalidation on crash). Because eviction is idempotent and an unnecessary eviction costs exactly one cache miss, **at-least-once is strictly the better side, and the asymmetry is the single most useful fact in this document.** Design so that duplicate application is free, and the hard problem evaporates.
3. **The commit→apply gap cannot be closed, only shortened.** If the process dies after `COMMIT` returns and before the eviction lands, the invalidation is gone. `System.Transactions` names this window explicitly: `IEnlistmentNotification.InDoubt` exists precisely because a volatile participant can be left not knowing the outcome. The only durable fix is to record the invalidation intent *in the same transaction* (an invalidation outbox, §6) — which does not make it atomic, it makes it **recoverable**, at the cost of a row per write and a dispatcher hop of latency.
4. **Cross-node L1 coherence is not achievable from inside a transaction.** Shipped HybridCache doc: *"When invalidating cache entries by key or by tags, they're invalidated in the current server and in the secondary out-of-process storage. **However, the in-memory cache in other servers isn't affected.**"* On a multi-instance deployment the coherence floor is `LocalCacheExpiration`, full stop. Nothing the transaction does changes it. (See §7 for the one contradicting source and why it matters.)
5. **Read-your-writes inside `T` + invisibility outside `T` cannot both be served from a shared cache.** These are contradictory requirements over one storage location. The in-transaction read must come from a **transaction-private** store (identity map / scoped overlay), never from L1/L2 (§9).

---

## 2. The four windows where a cache entry can go wrong

Useful as a checklist — most designs close 1 and 2 and silently ignore 3 and 4.

| # | Window | Effect | Closed by |
|---|---|---|---|
| 1 | Value written to cache inside `T`, `T` rolls back | **Phantom** — a value the DB never committed, live for other transactions | Never write inside `T` (§3) |
| 2 | Value written to cache inside `T`, `T` still open | **Dirty read through the cache** — an isolation violation, not staleness | Never write inside `T` |
| 3 | `T` commits, eviction hasn't landed yet | Stale-but-once-real value | Shorten (drain immediately post-commit); bound by TTL |
| 4 | Concurrent reader loaded the pre-image before `T` committed, and `SET`s it *after* `T`'s eviction | **Stale forever until TTL** — the classic cache-aside "stale set" race | Not closed by transaction-awareness at all (§4) |

Window 4 is the one that survives a perfect transactional design, and it is the reason "invalidate vs update" is not the whole question.

---

## 3. Defer-to-commit vs compensate-on-rollback vs both

### compensate-on-rollback (evict on failure)

- **Fails on kill.** SIGKILL, OOM-kill, pod eviction, host loss — the compensation code never runs. The phantom (window 1) survives to TTL.
- **Fails on rollbacks it doesn't see.** Rollback-to-savepoint (EF does this automatically, §8), a server-side abort (serialization failure, statement timeout, deadlock victim), a dropped connection — the transaction is gone without the compensating path being invoked.
- **Fails when the compensation fails.** Redis unavailable at exactly the moment of rollback → phantom persists, and there is no retry queue for it.
- **Is wrong even on the happy path.** Between the in-transaction write and the commit, every reader observes uncommitted state (window 2). This is worse than staleness: it breaks isolation for readers who never touched the transaction.
- **Verdict: not viable alone.** Its entire value is undoing a mutation that should not have happened.

### defer-to-commit (buffer, apply after commit)

- **Windows 1 and 2 cease to exist** — there is nothing to undo, so rollback is `buffer.Clear()`. This is the whole point.
- **Failure mode is window 3 only** — the commit→apply gap. Bounded by how fast the drain runs, unbounded on process death.
- **Second failure mode: unbounded buffer.** A batch job dirtying 10⁶ rows accumulates 10⁶ intents. Mitigation: de-duplicate by key on insert; collapse to *tag* invalidation above a threshold (`RemoveByTagAsync` is O(1) per tag, §7); cap + fall back to a coarse tag.
- **Third: it does not serve read-your-writes** (§9) — a later layer in the same transaction gets the pre-image unless a private overlay exists.

### both — and what "both" actually means

"Both" is not two alternatives OR'd together. The useful composition is three legs:

1. **Optional pre-commit evict** — evict `k` when the write is issued. On rollback this cost one cache miss; nothing is corrupted. It narrows (does not close) window 4 by shrinking the interval in which a concurrent reader's stale `SET` can win.
2. **Deferred post-commit evict** — the correctness leg (G2).
3. **Rollback = discard the buffer** — the "compensation", which is free precisely because leg 2 never fired.

Legs 1+2 are exactly Hibernate's `NONSTRICT_READ_WRITE`: *"when an item is updated, the cache is invalidated both before and after completion of the updating transaction"*, with the honest caveat the Hibernate docs themselves attach — *"strong consistency is not guaranteed and there is a small time window in which stale data may be obtained from the cache."* The recommendation here converges on a strategy that has been in production for two decades, and inherits its documented weakness rather than pretending to beat it.

---

## 4. Invalidate vs update — is writing the new value on commit ever safe?

**On this repo's `ICache` surface: no. Eviction is the only sound move.** Two independent reasons, and the second is decisive.

**Reason 1 — the cache-aside stale-set race (window 4).** With two concurrent actors:

```
Reader R:  miss on k → SELECT k → sees v0 → [GC pause / network stall]
Writer W:  UPDATE k = v1 → COMMIT → evict k
Reader R:  resumes → SET k = v0        ← stale, and now nothing will invalidate it
```

The entry is wrong until TTL, and no transaction-aware mechanism prevents it — R's `SET` is not part of any transaction. This is the canonical result from *Scaling Memcache at Facebook* (NSDI 2013) and the origin of the "**delete, don't set**" rule and of leases. Writing `v1` on commit does not fix it either: two committing writers can apply their post-commit `SET`s in the opposite order to their commit order, leaving the older value resident.

**Reason 2 — a safe update requires a conditional put, which `ICache` cannot express.** Writing the new value is safe under exactly one condition: the payload carries a monotonic version and the cache performs a **compare-and-set** (overwrite only if incoming version > stored version). That is precisely Hibernate `READ_WRITE`'s rule — an item is *"writable only if the incoming entity version is newer"*. The repo has the version material already (`IVersioned` with the counter bumped in `AppDbContextBase.SaveChanges` at `src/Data/EntityFrameworkCore/AppDbContextBase.cs:59`, plus `IRowVersioned` and `IHasXmin`) — but the cache side has nothing:

- `ICache.SetAsync` (`src/Caching/Core/ICache.cs:29`) is unconditional. No CAS, no `SetIfNewer`, no version parameter on `CacheEntryOptions`.
- `HybridCache` has no CAS primitive either, and `IDistributedCache` cannot express one. Implementing it means a Redis Lua script — which breaks out of the `HybridCache`/`IDistributedCache` abstraction and silently disables L1 correctness (L1 has no CAS at all).

**Where "update on commit" would still be legitimate:** immutable/append-only entities (Hibernate's `READ_ONLY` case), and single-writer-per-key domains. Both are policy assertions the entity owner must make explicitly — not a default.

**The middle tier, if eviction alone ever proves too expensive:** a *soft lock*, Hibernate `READ_WRITE`-style — write a sentinel entry at write time; readers who see the sentinel bypass the cache and hit the DB; the sentinel is replaced (or dropped) after commit. This **is** expressible over `ICache` (a `CacheEntry<T>` union of `Value | SoftLock`) without CAS, at the price of: every read paying a discriminated-union check, and a crash leaving a lock resident until its own TTL — Hibernate's default lock timeout is **60 seconds**, chosen for exactly that reason. Report it as a known tier, not a starting point.

---

## 5. The enlistment mechanism — how an inner layer registers a post-commit action without knowing the owner

Survey, scored against the two hard requirements: **(a)** works for EF *and* Dapper, **(b)** the inner layer's keys never surface above it.

| Mechanism | Fires on | Verdict |
|---|---|---|
| `System.Transactions`: `Transaction.Current.EnlistVolatile(IEnlistmentNotification, …)` → `Prepare`/`Commit`/`Rollback`/`InDoubt`; or the simpler `Transaction.TransactionCompleted` event | true transaction outcome | **Right shape, wrong cost.** Requires the app to open a `TransactionScope`/`CommittableTransaction` — EF's own `BeginTransaction` does *not* create an ambient transaction, so `Transaction.Current` is `null` in the normal path. Adopting `TransactionScope` imports: `TransactionScopeAsyncFlowOption.Enabled` required or the ambient doesn't flow across `await`; *"TransactionScope does not support async commit/rollback"* (sync-blocks the thread); provider-dependent (*"if a provider does not implement support… calls to these APIs may be completely ignored"*); distributed = Windows-only .NET 7+. **Reject as the primary seam; its `InDoubt` callback is however the honest proof of ceiling 3.** |
| `DbContext.Database.CurrentTransaction` | nothing — it's a property | **Observation only, and unreliable observation.** Returns `null` when EF is running an implicit SaveChanges transaction, and since EF 7 `AutoTransactionBehavior.WhenNeeded` (the default) may not create a transaction *at all* for a single-command save. An inner layer cannot even reliably answer "am I in a transaction?" this way. |
| `ISaveChangesInterceptor` — `SavedChanges` / `SaveChangesFailed` | after the SQL flush | **A safe *collect* point, not a safe *apply* point.** With no explicit transaction, `SavedChanges` is effectively post-commit. **Inside an explicit transaction it fires before `CommitAsync`** — applying there reproduces the original bug exactly. Per the EF interceptor table this is a *non-singleton* interceptor, so it may hold per-context state. |
| `IDbTransactionInterceptor` — `TransactionCommitted` / `TransactionRolledBack` / `RolledBackToSavepoint` / `TransactionFailed` | true commit/rollback/savepoint | **The correct EF-native apply point** — and the only hook that sees savepoint rollback (§8). Silent when no explicit transaction exists, so it must be paired with the SaveChanges hook. Also non-singleton. |
| **Ambient scoped buffer** (DI-scoped service, or `AsyncLocal` for non-DI reach) | whatever the owner drives | **The only mechanism that satisfies both (a) and (b).** Transport-agnostic: EF, Dapper, or a third store all record into the same buffer. Requires an owner (the pipeline's unit-of-work) to call complete/discard, and needs a defined degradation when nobody owns it. |
| **Outbox** (the repo already has one: `src/Messaging/Reliability/Ef/EfOutbox.cs`, `OutboxDispatcher`, `PostgresSkipLockedOutboxClaimStrategy`) | dispatcher, post-commit | **The same shape, made durable.** Intent recorded transactionally → applied after commit → idempotent → at-least-once. It is the outbox pattern's own answer to exactly this problem, and the answer is: *you don't get atomicity, you get recoverability.* |

**Pick: ambient scoped buffer as the seam; EF interceptors as the trigger; outbox as the optional durability leg.** The buffer is the abstraction the inner layer talks to; the interceptors are how the buffer learns the outcome without the inner layer knowing who owns the transaction.

### Sketch of the seam, in interface terms

```
// --- inner layer: owns keys, never exposes them ------------------------------
ITransactionalCache : ICache            // scoped; same read surface as ICache
    ValueTask InvalidateOnCommitAsync(string key, CancellationToken ct)
    ValueTask InvalidateByTagOnCommitAsync(string tag, CancellationToken ct)
    // NOTE: no SetAsync-on-commit overload. Deliberate — §4.

// --- the enlistment seam: an inner layer registers intent without an owner ---
ICacheEnlistment                        // scoped
    bool IsEnlisted { get; }            // false ⇒ apply-now semantics
    int  Depth { get; }                 // savepoint depth at time of recording
    void OnCommit(Func<CancellationToken, ValueTask> action)   // MUST be idempotent
    void OnRollback(Func<CancellationToken, ValueTask> action) // rare; default = discard

// --- the owner: the only thing an outer layer touches -----------------------
IUnitOfWork
    ValueTask<T> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> body, CancellationToken ct)
    // begin → body → SaveChanges → Commit → drain(OnCommit)
    // on throw: Rollback → discard buffer (no OnCommit runs)

// --- how the buffer learns the outcome, per store ---------------------------
CacheEnlistmentTransactionInterceptor : IDbTransactionInterceptor, ISaveChangesInterceptor
    TransactionCommitted     → drain
    TransactionRolledBack    → discard all
    RolledBackToSavepoint    → discard intents with Depth > savepoint depth
    SavedChanges             → drain iff CurrentTransaction is null (implicit-tx path)
    SaveChangesFailed        → discard intents from this SaveChanges only (§8)
```

Rules the seam must carry, each of which is a finding rather than a preference:

- **Registered scoped.** The repo's `ICache` is a **singleton** (`TryAddSingleton<ICache, HybridCacheAdapter>`, `src/Caching/Hybrid/HybridCachingServiceCollectionExtensions.cs:40`), so a transactional decorator cannot be a plain singleton decorator over it. Cleanest shape: leave `ICache` singleton and immediate; add a *separate scoped* `ITransactionalCache` that pipeline layers consume. Swapping `ICache` itself to scoped would silently change semantics for every existing consumer.
- **`IsEnlisted == false` ⇒ write through immediately.** This is the explicit degradation NHibernate lacks — its second-level cache *"requires the use of transactions… interacting with the data store without an explicit transaction will not allow the second level cache to work as intended"*, which is a silent trap. Degrade loudly and deliberately instead.
- **De-duplicate intents by key/tag.** A key invalidated five times in one transaction is one eviction.
- **Drain after commit returns, never inside the commit path.** A drain failure must be logged + metered and **must not** be rethrown into the command result — the database is already committed, and turning a success into a failure is a lie the caller will act on. This is the silent-staleness path, so it needs its own counter.
- **Order within the drain is irrelevant** (evictions commute) — which is what makes at-least-once free.

---

## 6. The crash window, and what the outbox buys

Process dies between `COMMIT` and drain → the invalidation is lost → stale until TTL. Three honest options, in ascending cost:

| Option | Window on crash | Cost |
|---|---|---|
| Accept it; rely on TTL | ≤ `Expiration` | zero |
| Drain from a durable process-local journal (fsync'd) | ≤ restart time | fsync per transaction; still lost on host loss |
| **Invalidation outbox** — stage intents as rows in the same transaction, dispatch post-commit | ≤ dispatcher poll interval | +1 row per write, +dispatcher latency, +table |

The third is the repo's own existing machinery pointed at a new payload — `EfOutbox` already *"adds a row to the tracked context — it commits atomically with the business write on the app's SaveChanges, solving the dual-write problem with no distributed transaction"* (`src/Messaging/Reliability/Ef/Ef.md`), and `PostgresSkipLockedOutboxClaimStrategy` already handles multi-instance claim. **Do not build a second dispatcher.**

MassTransit's two outboxes are the exact same fork, and its documentation states the trade plainly: the **in-memory outbox** buffers until the consumer completes and buffered messages are **lost entirely if the process terminates** (best-effort, "for maximum throughput when retries are enough"); the **transactional outbox** persists in the DB transaction and messages *"can be delivered later, even if the process restarts"*, yielding at-least-once + inbox dedupe. A deferred cache buffer is an in-memory outbox with a different payload — and inherits its guarantee exactly.

---

## 7. Distributed cache — the honest ceiling

**Shipped-behavior facts (MS docs):**

- `RemoveAsync` — *"When an entry is removed, it is removed from both the primary and secondary caches."*
- `RemoveByTagAsync` — *"Calling `RemoveByTagAsync` doesn't remove values from either the local or distributed cache. Instead, it establishes an 'ignore anything created before this point' rule for entries associated with that tag."* Old values stay resident until natural expiry; the staleness check happens on read.
- The blanket note: *"When invalidating cache entries by key or by tags, they're invalidated in the current server and in the secondary out-of-process storage. **However, the in-memory cache in other servers isn't affected.**"*

**Consequences for this design:**

1. On a **single-instance** deployment, post-commit `RemoveAsync` gives real coherence modulo the crash window. Most products in this portfolio start here — say so, and don't over-engineer for a topology that doesn't exist yet.
2. On a **multi-instance** deployment, `LocalCacheExpiration` **is** the coherence budget, per entity family. `CacheEntryOptions.LocalCacheExpiration` already exists (`src/Caching/Core/CacheEntryOptions.cs:14`); what's missing is a *policy layer* that sets it per entity rather than per call site.
3. Shrink options: (a) `LocalCacheExpiration = TimeSpan.Zero` for transactionally-managed keys — L2-only, pay the Redis hop, get near-coherence; (b) swap the `HybridCache` implementation for **FusionCache**, the first third-party `HybridCache` implementation, which ships a **backplane** that publishes an invalidation notification to every node on `Set`/`Remove` — already on the SDK's caching roadmap (`src/Caching/Caching.md` §Roadmap); (c) accept the window.
4. **Tag invalidation is the natural drain granularity** *and* it doesn't leak keys upward — one tag per aggregate/table beats N keys, is O(1) at the cache, and caps buffer growth.

**Open question, load-bearing, unresolved from documentation alone:** the HybridCache tag-invalidation design ([dotnet/extensions#7098](https://github.com/dotnet/extensions/issues/7098)) describes propagating tag-invalidation timestamps to other nodes via the L2 backend's pub/sub (a Redis channel `__MSFT_DC__TagInvalidation`), with each read comparing an entry's creation time against the tag's invalidation time. If that shipped, **tag invalidation is cross-node while key removal is not** — which would make the entire post-commit drain tag-based rather than key-based. The shipped doc's blanket note says otherwise. **Verify empirically against the installed `Microsoft.Extensions.Caching.Hybrid` before choosing the drain granularity** — this single fact changes the design.

**At-most-once vs at-least-once, named:** applying *before* commit is at-most-once with respect to correctness — but note the asymmetry: for eviction, a premature apply is *harmless* (you evicted a still-valid value), merely insufficient, because a concurrent reader can repopulate before the commit lands. Applying *after* commit is at-least-once and correct modulo the crash window. **Doing both is cheap and strictly better than either**, which is why §3's three-leg composition is the recommendation.

---

## 8. Nested transactions and savepoints

- **EF creates savepoints automatically:** *"When `SaveChanges` is invoked and a transaction is already in progress on the context, EF automatically creates a savepoint before saving any data… If `SaveChanges` encounters any error, it automatically rolls the transaction back to the savepoint, leaving the transaction in the same state as if it had never started."* So **"an inner layer rolls back while the outer commits" is not a hypothetical — it is EF's default behavior on every failed `SaveChanges` inside a transaction.**
- **Therefore a flat buffer is wrong.** Intents must be depth-tagged (or held on a stack), so `RolledBackToSavepoint` / `SaveChangesFailed` discards only the intents recorded above that savepoint and keeps everything below it. A flat buffer either over-discards (loses valid invalidations from committed work → stale entries) or under-discards (drains invalidations for work that was rolled back → harmless, just wasteful). The failure direction is asymmetric and worth exploiting: **when in doubt, over-invalidate.**
- **Postgres detail that makes savepoints non-optional here:** any error inside a transaction poisons it — subsequent statements fail until `ROLLBACK TO SAVEPOINT`. A layered pipeline that expects an inner layer's failure to be recoverable *must* be savepoint-based; there is no other shape.
- **Known savepoint caveat:** on SQL Server, savepoints are incompatible with MARS — EF won't create them when MARS is enabled, *"even if MARS is not actively in use"*, and *"if an error occurs during SaveChanges, the transaction may be left in an unknown state."* Postgres-first stack, so this is a portability footnote rather than a live risk.
- **`TransactionScope` nesting is a different animal.** `TransactionScopeOption.RequiresNew` creates a genuinely independent transaction that can commit while the outer rolls back. A scope-keyed buffer gets this wrong; correctness would require keying by transaction identity. **Recommend declaring `RequiresNew` out of scope** rather than half-supporting it.

---

## 9. Read-your-writes inside the same transaction

The requirement — *a later layer in the same transaction must see the earlier layer's write, but nobody outside may* — is unsatisfiable over one shared storage location (ceiling 5). The resolution:

- **The in-transaction read must be served from a transaction-private overlay**, consulted *before* L1/L2 and invisible to other scopes. EF's change tracker already is exactly this for EF-loaded entities; Dapper has nothing equivalent — which is why research question 4 (request-scoped read cache / identity map) and this question are **the same mechanism seen from two sides**.
- **The dirty-key set must also act as a read barrier.** Inside `T`, a read of a dirtied key must *not* be served from L1/L2 — those tiers hold the pre-image, which is correct for everyone else and wrong for `T`. So the buffer is not merely an invalidation list; it is simultaneously (a) the post-commit drain list and (b) the set of keys the shared cache must be bypassed for. **One structure, two jobs** — worth designing as such rather than discovering later.
- **Prior art's cheaper answer:** EFCoreSecondLevelCacheInterceptor simply refuses the problem — *"To avoid complications, all queries inside an explicit transaction (`context.Database.BeginTransaction()`) will **not** be cached by default"* (overridable via `AllowCachingWithExplicitTransactions(true)`). Correct, trivial, and forfeits in-transaction cache benefit entirely. It is a legitimate v1.
- **Marten's answer is structural:** sessions implement the unit-of-work pattern with all mutations queued and flushed in a single `SaveChangesAsync`, over a session-scoped **identity map**; there is no shared cross-session second-level cache in the documented surface. A rollback cannot leak, because the "cache" was never shared in the first place. **This is the cheapest correct design of all** — make the in-transaction store private, and let the shared store be invalidate-only.

---

## 10. Prior art — what each stack actually does

| Stack | Its answer to *this exact* problem | Transferable? |
|---|---|---|
| **Hibernate `READ_ONLY`** | Immutable entities only — no writes, no problem | Yes, as an entity-level policy |
| **Hibernate `NONSTRICT_READ_WRITE`** | No locks; **invalidate both before and after** transaction completion. Docs are explicit that strong consistency is not guaranteed and a small stale window remains | **Yes — this is the §3 recommendation** |
| **Hibernate `READ_WRITE`** | Soft lock written before commit; after commit `afterUpdate` replaces the lock with the new value; delete leaves a lock with an extended timeout; readers seeing a lock go to the DB; item writable only if the incoming version is newer. **A crashed process leaves the lock until its ~60s timeout** — the crash window handled by lock TTL, not durability | Partially — the soft lock is expressible over `ICache`; the versioned put is not (§4) |
| **Hibernate `TRANSACTIONAL`** | Real 2PC via a JTA/XA-capable cache provider | **No** — no XA between Postgres and Redis on .NET (ceiling 1) |
| **NHibernate** | Same four strategies; 2L cache *requires* explicit transactions to behave as intended; since NH 5.0 using the session connection inside `AfterTransactionCompletion` throws | Yes as a warning: make the no-transaction degradation explicit |
| **EFCoreSecondLevelCacheInterceptor** | Sidesteps it: **no caching inside an explicit transaction by default**; invalidation is *table-level* on `SaveChanges` (all cached queries depending on a modified table). Known hole: `ExecuteUpdate`/`ExecuteDelete` don't trigger interceptors → silent staleness unless invalidated manually | Yes — both the sidestep and the hole |
| **Marten** | Session-scoped identity map + unit of work flushed at `SaveChangesAsync`; no shared second-level cache. Rollback can't leak because nothing shared was written | **Yes — structurally the cheapest correct answer** |
| **MassTransit in-memory outbox** | Buffer released after the consumer completes; **lost entirely on process death**; documented as best-effort, pair with retry | Yes — it *is* the deferred-buffer design, with its guarantee stated honestly |
| **MassTransit transactional outbox** | Intent persisted in the business transaction, delivered by a hosted service, survives restart; at-least-once + `InboxState` dedupe for exactly-once *effect* | Yes — §6 |
| **This repo's `EfOutbox`** | Already implements the durable leg: row added to the tracked context, commits atomically with the business write, dispatcher drains post-commit with `FOR UPDATE SKIP LOCKED` claim | **Already here — reuse, don't rebuild** |
| **Facebook memcache (NSDI 2013)** | `delete`, never `set`, on write; leases to defeat the stale-set race; TTL as backstop | Yes — the "delete not set" rule (§4); leases are not expressible over `ICache` |

---

## 11. Ground truth in this repo — what constrains the answer

| Fact | Location | Why it matters |
|---|---|---|
| `ICache` registered **singleton** | `Caching/Hybrid/HybridCachingServiceCollectionExtensions.cs:40` | A transactional decorator can't be a singleton decorator; needs a separate scoped type |
| `ICache` surface = `GetOrCreate`/`Set`/`Remove`/`RemoveByTag` — **no CAS, no conditional set, no batch remove** | `Caching/Core/ICache.cs` | Kills versioned write-on-commit (§4); the `HybridCache` multi-key `RemoveAsync` overload isn't surfaced, so a drain is N calls today |
| `CacheEntryOptions` carries `Expiration` / `LocalCacheExpiration` / `Tags` — **no version, no policy layer** | `Caching/Core/CacheEntryOptions.cs` | Coherence budget is per-call-site ad hoc; needs per-entity policy (§7) |
| `EfRepository` calls `SaveChangesAsync` **on every single write** | `Data/EntityFrameworkCore/Repositories/EfRepository.cs:50,59,67,75,86` | **There is no rollback boundary to defer to today.** Each write is its own implicit transaction. The question is literally unanswerable until a unit-of-work exists |
| **No transaction abstraction anywhere in `src/Data`** — grep finds no `IDbContextTransaction`, `BeginTransaction`, `TransactionScope`, or savepoint use | `src/Data/**` | Greenfield seam; nothing to retrofit around |
| `DapperRepository` opens **its own connection per operation** from the shared `NpgsqlDataSource` | `Data/Dapper/Repositories/DapperRepository.cs:49,86,100,110,126` + `Data/Abstractions/DataSourceConnectionFactory.cs:14` | **Dapper writes cannot join EF's transaction today.** Two commit points ⇒ "commit" is ambiguous for the buffer. Must be resolved first (§12.8) |
| `AddEfInterceptor<T>` registers **singleton**, auto-wired into every SDK context | `Data/EntityFrameworkCore/Interceptors/EfInterceptorServiceCollectionExtensions.cs:22` → `EntityFrameworkCoreServiceCollectionExtensions.cs:66` | EF's own table marks `ISaveChangesInterceptor`/`IDbTransactionInterceptor` as *non*-singleton; the SDK's helper forces singleton, so the interceptor cannot hold scoped state — it must reach the buffer through an ambient/accessor |
| `AddEntityFrameworkCore<T>` defaults `UsePooling = true` (`AddDbContextPool`); `AddPostgresPersistence` uses plain `AddDbContext` | `EntityFrameworkCoreOptions.cs:7` · `PostgresPersistenceServiceCollectionExtensions.cs:51` | **Two different capability levels.** With `AddDbContextPool` the options callback resolves from the root provider — scoped services are unavailable there. With `AddDbContext` the scoped provider is available. A design relying on scoped resolution in the options callback works on one path and not the other |
| `IVersioned.Version` bumped on save; `IRowVersioned`, `IHasXmin` exist | `Data/EntityFrameworkCore/AppDbContextBase.cs:59` · `Data/Abstractions/` | The version material for a Hibernate-`READ_WRITE`-style conditional put exists on the **entity** side; only the cache side is missing |
| `EfOutbox` + `OutboxDispatcher` + `PostgresSkipLockedOutboxClaimStrategy` + `IInboxProcessor` all shipped | `Messaging/Reliability/Ef/` | The durable leg (§6) is a payload change, not a new subsystem |
| `IdempotencyBehavior` already caches command responses in the mediator pipeline | `Mediator/Idempotency/IdempotencyBehavior.cs:85` | Precedent for cache writes on the command path — and note it `Store`s **after** `nextStep()` returns, i.e. it has the same commit-ordering question, currently unexamined |

---

## 12. What this forces on the pipeline design

1. **The pipeline must own an explicit transaction.** Today's SaveChanges-per-write means no rollback boundary exists. Cache coherence is downstream of a unit-of-work; there is no ordering in which it can be solved first.
2. **Inner layers must not be able to `Set` the shared cache inside a transaction.** Enforce it in the *surface*, not in a guideline: the facade handed to pipeline layers offers `InvalidateOnCommit`, not `SetOnCommit`. (Alternative: `SetAsync` inside an enlisted scope silently degrades to "evict on commit" — safer, but surprising; prefer the explicit surface.)
3. **The intent buffer is savepoint-aware (depth-tagged), not flat** — because EF auto-savepoints every `SaveChanges` inside a transaction and rolls back to them on failure (§8).
4. **The buffer doubles as the read barrier**, so it must be designed jointly with research question 4's request-scoped read cache. One structure, two jobs (§9).
5. **Every deferred action must be idempotent** — at-least-once is the only achievable delivery, and idempotence is what makes it free (ceiling 2).
6. **Drain failures must never fail the command.** Metered + logged, TTL as backstop. This needs an explicit observability surface (`cache.drain.failed` counter, at minimum) because it is the one silent-staleness path in the design.
7. **`CacheEntryOptions` needs a per-entity policy layer.** In a multi-instance deployment `LocalCacheExpiration` *is* the coherence budget, and it cannot be a per-call-site guess (§7).
8. **Resolve the Dapper/EF transaction seam before the cache seam.** Either the pipeline hands Dapper the EF `DbConnection` + `DbTransaction` (`context.Database.GetDbConnection()` / `CurrentTransaction.GetDbTransaction()`), or there are two independent commit points and "post-commit" is undefined. This is a **precondition**, not a parallel workstream.
9. **Raw SQL, `ExecuteUpdate`, and `ExecuteDelete` bypass the whole mechanism** — they skip change tracking and (for the bulk operators) interceptors entirely. Either ban them on cached-entity paths or require explicit invalidation. EFCoreSecondLevelCacheInterceptor has exactly this hole today; inheriting it knowingly is fine, inheriting it accidentally is not.
10. **Registration shape:** `ICache` stays singleton + immediate (existing consumers unaffected); add scoped `ITransactionalCache` + `ICacheEnlistment`; the EF interceptor reaches the scoped buffer through an ambient accessor, because the SDK's `AddEfInterceptor<T>` pins interceptors to singleton and `AddDbContextPool` removes scoped resolution from the options callback.
11. **Verify HybridCache's cross-node tag semantics empirically before choosing key-based vs tag-based drain** (§7, open question). This is the highest-leverage unknown in the document.
12. **State the guarantee in the docs the way §0 states it.** "Transaction-aware caching" invites readers to assume coherence. G1/G2/G3 is what ships.

---

## 13. Sources

Primary:

- [HybridCache library in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0) — `RemoveAsync`/`RemoveByTagAsync` semantics, "the in-memory cache in other servers isn't affected", stampede protection
- [Transactions — EF Core — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/saving/transactions) — automatic savepoints, `System.Transactions` limitations, MARS caveat, cross-context transaction sharing
- [Interceptors — EF Core — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors) — `ISaveChangesInterceptor` / `IDbTransactionInterceptor` hooks and singleton-ness table
- [DatabaseFacade.AutoTransactionBehavior — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.infrastructure.databasefacade.autotransactionbehavior?view=efcore-9.0) — `WhenNeeded` default since EF 7
- [IEnlistmentNotification — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.transactions.ienlistmentnotification?view=net-5.0) — `Prepare`/`Commit`/`Rollback`/`InDoubt` volatile-enlistment contract
- [CacheConcurrencyStrategy — Hibernate javadocs](https://docs.jboss.org/hibernate/orm/6.0/javadocs/org/hibernate/annotations/CacheConcurrencyStrategy.html) — the four strategies
- [How does Hibernate READ_WRITE CacheConcurrencyStrategy work — Vlad Mihalcea](https://vladmihalcea.com/how-does-hibernate-read_write-cacheconcurrencystrategy-work/) — soft-lock protocol, lock timeout, version-gated writes
- [How does Hibernate NONSTRICT_READ_WRITE CacheConcurrencyStrategy work — Vlad Mihalcea](https://vladmihalcea.com/how-does-hibernate-nonstrict_read_write-cacheconcurrencystrategy-work/) — invalidate-before-and-after
- [Transactions and Concurrency — NHibernate reference](https://nhibernate.info/doc/nhibernate-reference/transactions.html) — 2L cache requires explicit transactions
- [EFCoreSecondLevelCacheInterceptor — GitHub](https://github.com/VahidN/EFCoreSecondLevelCacheInterceptor) — no caching in explicit transactions by default; table-level invalidation; `ExecuteUpdate`/`ExecuteDelete` hole
- [Outbox Configuration — MassTransit](https://masstransit.io/documentation/configuration/middleware/outbox) — in-memory vs transactional outbox guarantees
- [Opening Sessions — Marten](https://martendb.io/documents/sessions.html) — identity map + unit of work
- [HybridCache — tags and invalidation — dotnet/extensions#7098](https://github.com/dotnet/extensions/issues/7098) — tag-invalidation design, L2 pub/sub propagation (**conflicts with the shipped doc — verify**)
- [FusionCache backplane — GitHub](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md) — cross-node L1 invalidation; FusionCache as a `HybridCache` implementation

In-repo:

- `src/Caching/` — `Core/ICache.cs`, `Core/CacheEntryOptions.cs`, `Hybrid/HybridCacheAdapter.cs`, `Hybrid/HybridCachingServiceCollectionExtensions.cs`, `Caching.md`
- `src/Data/` — `EntityFrameworkCore/{AppDbContextBase,EntityFrameworkCoreServiceCollectionExtensions,EntityFrameworkCoreOptions}.cs`, `EntityFrameworkCore/Repositories/EfRepository.cs`, `EntityFrameworkCore/Interceptors/EfInterceptorServiceCollectionExtensions.cs`, `Dapper/Repositories/DapperRepository.cs`, `Abstractions/DataSourceConnectionFactory.cs`, `PostgresPersistenceServiceCollectionExtensions.cs`
- `src/Messaging/Reliability/Ef/` — `EfOutbox.cs`, `OutboxDispatcher.cs`, `PostgresSkipLockedOutboxClaimStrategy.cs`, `Ef.md`
- `src/Mediator/Idempotency/IdempotencyBehavior.cs`
