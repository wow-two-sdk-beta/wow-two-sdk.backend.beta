# Data pipeline — idea capture

*Captured 2026-07-19. Owner's idea, recorded before analysis. Not a design — the design comes after the research pass.*

> Sibling of the events layer's `IConsumeFilter` chain, applied to **data read & write**: an ordered, layered pipeline over EF Core + Dapper + caching, where each layer can participate in the transaction and in cache coherence without the layers above it knowing what it manages.

## What exists today

- CQRS repository split — reads via Dapper (`IReadRepository<TEntity, TId>`), writes via EF (`IWriteRepository<TEntity, TId>`); `AddCqrsRepositories`
- shared `NpgsqlDataSource` consumed by **both** EF Core and Dapper, so they can share a connection
- `Caching/` — `ICache`, `CacheKeyBuilder`, HybridCache adapter (L1 memory + L2 Redis)
- entity abstractions incl. `IHasXmin`, `IRowVersioned`, `IVersioned`, `IAuditable`, `ISoftDeletable`
- **no pipeline** — nothing composes these; a caller picks a repository and calls it

## The problems (owner's framing)

### 1. Transaction scope is too narrow

EF opens a transaction only around `SaveChanges`. Work that should be in the same transaction — other layers' writes, Dapper commands, outbox rows — isn't automatically enrolled.

Previously solved by threading save-changes through each layer via a `CommandOptions` parameter. That works, but see problem 2.

### 2. Cache coherence breaks under rollback *(the core problem)*

- an inner layer writes a row inside a transaction, then writes its cache entry
- an outer layer fails; the transaction rolls back
- **the cache entry survives** — it now holds a value the database never committed
- the outer layer neither knows about that cache entry nor should have to: the inner layer owns it

So cache writes must be **transaction-aware** — deferred to commit, or compensated on rollback — without leaking the inner layer's keys upward.

### 3. Duplicate reads across validation layers

Validation happens twice: once in infrastructure/persistence, once in presentation. Each fetches the previous version of the record to compare against.

That's **two round trips for the same row**. Read it once (Dapper), cache it for the request, and let every later layer reuse it.

### 4. Dapper read → EF write handoff

If the previous version is already fetched via Dapper and cached, the write should reuse it: **attach** the entity to EF's change tracker rather than re-loading it.

Open question the owner raised, unresolved:

- can EF attach a **partial** entity (only the columns Dapper actually selected), or does attaching require a **full** entity?
- if full-only, Dapper needs a "materialize the complete entity" mode so the attach is legal
- if partial works, what are the constraints — concurrency tokens, shadow properties, owned types, navigations?

This is the question the research pass has to answer first: it decides whether the read and write halves can share a materialized entity at all, and everything else in the design depends on the answer.

## Scope hints

- EF Core + Dapper + caching now; other integrations later if they earn it
- the pipeline shape is the deliverable, not any single integration
- follows the SDK doctrine: build the vector to completeness once, so the next product finds it there

## Open questions for the research pass

1. **EF attach semantics** — partial vs full entity; concurrency tokens; shadow properties; owned types; navigations. Decides the whole read→write handoff.
2. **Transaction-aware caching** — defer-to-commit vs compensate-on-rollback vs both. What does the ambient transaction look like, and how does a layer enlist without the layers above it knowing?
3. **Where does the pipeline sit** — around the repository, around a unit of work, or around the mediator command? Each has a different blast radius.
4. **Request-scoped read cache** — an identity map is the classic answer (EF has one; Dapper doesn't). Is the answer "make Dapper reads populate EF's identity map", or a separate per-request store?
5. **Prior art** — what do MassTransit-adjacent stacks, Marten, Linq2Db, ServiceStack, NHibernate, EF's own `DbContext` pooling, and the outbox pattern already solve here? Where is this genuinely novel vs a reinvention?
6. **Failure modes** — what does this break that a simple repository doesn't? Nested transactions, savepoints, long-running requests, multi-DbContext, read replicas, distributed transactions.

## Sequencing note

Recorded alongside the events layer, which is at diminishing returns (Tier-B complete, Tier-C is adapter/format breadth). This vector is deeper and less explored — research first, decide after.
