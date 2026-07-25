# EF attach semantics — research

*Last updated: 2026-07-22 · Status: **RESEARCH ONLY** — no code written, no decision taken. Answers question **1** (partial vs full entity on attach) of [`data-pipeline-idea.md`](../data-pipeline-idea.md).*

> **How this was derived:** every behavioural claim below was executed, not recalled. Three throwaway probe programs were built in the scratchpad against the **exact pinned versions** — `Microsoft.EntityFrameworkCore` **10.0.3**, `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.0**, `Dapper` **2.1.66** (`src/Directory.Packages.props`) — on .NET SDK 10.0.300 against a real PostgreSQL 16 container, with `LogTo` capturing every emitted `DbCommand`. The model in the probe is a verbatim copy of this repo's own conventions (`ApplyNpgsqlConventions` xmin mapping, `EntityModelConventions` soft-delete filter + `IVersioned` token, `AuditInterceptor` timestamp logic). Doc claims cross-checked against EF Core docs (last updated 2025-10-30) and the EF 10 breaking-changes page. Probe transcript in §11, sources in §12.

---

## 0. Verdict

**Conditional — partial attach is viable, and it is the *only* viable shape. But it is conditional on never touching three APIs.**

- `Attach` + per-property modification **works on a partial entity** and emits a narrow `UPDATE` touching only the columns you name. Zero SQL is issued by `Attach` itself. The owner's goal — write without re-loading what Dapper already fetched — is achievable.
- `Update()`, `Entry(e).State = Modified`, and `OriginalValues.SetValues(...)` are **all fatal on a partial entity**. Each one writes CLR defaults over real data, silently, with no exception. `Update()` is the one the SDK's own write repository calls (`src/Data/EntityFrameworkCore/Repositories/EfRepository.cs:66`), so *the current `IWriteRepository` implementation is incompatible with a Dapper→attach handoff as written*.
- The hard floor is not "full entity" — it is **PK + concurrency token + the columns being written**. Everything else may be absent.
- ⚑ The sharpest specific finding: **PostgreSQL `SELECT *` does not return `xmin`** (it is a system column). `DapperRepository.GetByIdAsync` uses `SELECT *` (`src/Data/Dapper/Repositories/DapperRepository.cs:48`). So an `IHasXmin` entity read through the SDK's own Dapper repository *always* arrives with `Xmin = 0`, and attaching + saving it throws `DbUpdateConcurrencyException` **100% of the time**. The read half must name `xmin` explicitly.

**No EF Core 10 breaking change touches change tracking, `Attach`/`Update`, concurrency tokens, shadow properties, owned types, or query filters.** The EF 10 breaking-changes page lists ten items; all are tooling, SQL Server JSON typing, parameterized-collection translation, `ExecuteUpdateAsync`'s lambda type, complex-type column naming, a convention signature, a logger signature, and parameter naming. The semantics below are therefore continuous with EF 8/9.

---

## 1. API × what it writes × when it loses data

Probe row seeded as `name='REAL-NAME' desc='REAL-DESCRIPTION' price=99.50 category_id=<set> dim=10x20 note='REAL-NOTE' created_at=2020-01-01`.
Dapper "partial read" fetched **`id`, `name`, `xmin` only** — every other property left at its CLR default.

| API | entity state after | which properties get flagged modified | emitted `UPDATE` | data loss on a partial entity |
|---|---|---|---|---|
| `Attach(e)` | `Unchanged` | none | none until you mutate | **none** — safe |
| `Attach(e)` then mutate a CLR property | `Modified` | only the mutated ones (via `DetectChanges`) | `SET name = @p0` | **none** — safe |
| `Attach(e)` then `Entry(e).Property(p).IsModified = true` | `Modified` | only `p` | `SET name = @p0` | **none** — safe. Writes `p` even if its value is unchanged |
| `Entry(e).Property(p).CurrentValue = v` | `Modified` | only `p` | `SET <p> = @p0` | **none** — safe. Works for shadow properties too |
| `Update(e)` / `DbSet.Update(e)` | `Modified` | **every non-key CLR-mapped property**, incl. all owned-type properties when the owned instance is non-null | `SET category_id, created_at, deleted_at, description, is_deleted, name, price, updated_at` | **total** — 6 real columns replaced by `NULL` / `0` / `0001-01-01` |
| `Entry(e).State = EntityState.Modified` | `Modified` | identical to `Update()` | identical | **total** — identical |
| `Entry(e).OriginalValues.SetValues(dict)` on a **partial** attach | `Modified` | every property whose cached original ≠ the partial entity's default | `SET description=NULL, name, note=NULL, price=0, version=0` | **total** — and worse than `Update()`, because it also clobbers shadow properties |
| `Entry(e).OriginalValues.SetValues(dict)` on a **full** attach | `Modified` | only genuine diffs | narrow | **none** — this is the documented disconnected-update recipe |
| `Entry(e).CurrentValues.SetValues(incoming)` on a **full** attach | `Modified` | only genuine diffs | `SET price = @p0` | **none** — the best-fit recipe (§9.2) |
| `ExecuteUpdateAsync(...)` | not tracked at all | n/a | exactly the columns you set | **none** — but bypasses `SaveChanges`, so no interceptors (§9.3) |

Two rows of that table are the entire finding: **the modification-marking APIs are safe; the state-setting APIs are not.**

---

## 2. The silent-data-loss mechanism, precisely

`Attach` puts the entity in `Unchanged` and snapshots **the values the object currently holds** as the original values. It does not read the database — the probe confirmed `Attach` issues **0 SQL statements**. EF therefore has no idea that `Description` is `null` because Dapper never selected it, versus `null` because the caller cleared it. Those two are indistinguishable by construction.

`Update()` then flags every non-key property `Modified`, and `SaveChanges` writes every flagged property regardless of whether current ≠ original. Verified: in the probe, `description`, `price`, `created_at` all had current == original == CLR default and were *still* emitted in the `SET` list.

So the loss condition is exact:

> **`Update()` or `State = Modified` on an entity that was not fully materialized ⇒ every unfetched mapped column is overwritten with its CLR default, in one statement, with no error.**

`Attach` + explicit modification avoids it because *nothing* is flagged until you flag it. The absence of a flag is the protection.

Three corollaries that are easy to miss:

- ⚑ **`Update()` on an entity whose PK is at its CLR default becomes an `INSERT`, not an `UPDATE`.** Verified: `Id = Guid.Empty` with `ValueGeneratedOnAdd` (EF's default for `Guid` PKs) → state `Added` → `INSERT INTO gadgets ...` with a freshly generated UUIDv7. The docs are explicit: *"when using generated keys, EF Core will always insert an entity when that entity has no key value set."* Same for `Attach`. If the key property is configured `ValueGeneratedNever()`, the same code path yields `Unchanged` instead — so the behaviour flips on model configuration, not on the value.
- ⚑ **`Attach` tracks the whole reachable graph.** Verified: attaching a root with a collection navigation containing one child with a set key and one with `Guid.Empty` gave `Unchanged` for the first and **`Added`** for the second — i.e. `SaveChanges` would insert a phantom row. A Dapper multi-map read that leaves any child key at default turns a read into an insert.
- **An empty/unpopulated collection navigation is harmless.** Verified: attaching a root whose `Tags` list is empty and saving left the existing child rows untouched (`tags = 1` before and after). EF does not interpret an empty collection as "delete the children".

---

## 3. Concurrency tokens — `IHasXmin`, `IVersioned`, `IRowVersioned`

### 3.1 The rule

EF compares the token's **original value** — not its current value — in the `WHERE` clause. Docs: *"the value of the concurrency token on the database is compared against the original value read by EF Core."* On an attached entity, original value = whatever the object held at `Attach` time. So **the token must be populated by the Dapper read, before `Attach`.**

Emitted shape, verified, both for `Attach`+mutate and `Attach`+`IsModified`:

```sql
UPDATE products SET name = @p0
WHERE id = @p1 AND xmin = @p2
RETURNING xmin;
```

The token is in the `WHERE` for a selective update too — narrowing the `SET` list does **not** narrow the concurrency check. That is the good news.

### 3.2 Per-contract behaviour when the token is not fetched

| contract | token | value if unfetched | what happens on save | verdict |
|---|---|---|---|---|
| `IHasXmin` (Postgres) | `uint Xmin`, mapped `xid`, `ValueGeneratedOnAddOrUpdate` + `IsConcurrencyToken` (`src/Data/EntityFrameworkCore/Postgres/PostgresModelBuilderExtensions.cs:20-25`) | `0` | `WHERE xmin = 0` → 0 rows → **`DbUpdateConcurrencyException`** | **fails loud** — verified |
| `IVersioned` | `uint Version`, `IsConcurrencyToken` (`src/Data/EntityFrameworkCore/EntityModelConventions.cs:26`) | `0` | `WHERE version = 0`. A row whose real version is 0 — i.e. **any row never yet updated** — **matches**, and the write succeeds with no concurrency protection at all | ⚑ **fails silent for new rows** — verified |
| `IRowVersioned` (SQL Server) | `byte[] RowVersion`, `IsRowVersion()` (`src/Data/EntityFrameworkCore/SqlServer/SqlServerModelBuilderExtensions.cs:20-22`) | `null` | expected `WHERE [RowVersion] = @p` with a null parameter → never matches → `DbUpdateConcurrencyException` | **not verified** — no SQL Server instance was available. High confidence, but treat as unconfirmed |

The `IVersioned` case is the one worth designing against: it is the *only* one of the three where a missing token produces a wrong-but-successful write. It is bounded (only bites when the real version is 0) but it is exactly the "first update after insert" case, which is common.

Note the SDK increments `IVersioned` itself in `AppDbContextBase.IncrementConcurrencyVersions` (`:59-63`) — `versioned.Version++` on every `Modified` entry. That runs before `base.SaveChanges`, so on a correctly-seeded attach it produces `SET version = 8 WHERE version = 7`. Verified. It is a CLR-property mutation, so it depends on change detection running — see §4.3.

### 3.3 Injecting the token out-of-band (from a cache entry, after `Attach`)

The pipeline might want to carry the token separately from the entity. It can — but **both values must be set, not just the original**.

- setting `OriginalValue` alone: verified to produce `SET version = 0 ... WHERE version = 7` — current (0) ≠ original (7) marks the token itself `Modified`, so the token gets *written back as zero*. Silent corruption of the token.
- setting `OriginalValue` **and** `CurrentValue` to the same fetched value: correct `WHERE version = 7`, token not in the `SET` list (until the SDK's increment bumps it). Verified.

`entry.Property(x => x.Version).OriginalValue = v; entry.Property(x => x.Version).CurrentValue = v;` — both, always, or don't do it at all.

### 3.4 The `SELECT *` trap

PostgreSQL system columns are excluded from `SELECT *`. Verified directly:

```
probe=# select * from t;          →   id | name
probe=# select xmin, * from t;    →   xmin | id | name
```

`DapperRepository.GetByIdAsync` and `GetAllAsync` both issue `SELECT *` (`src/Data/Dapper/Repositories/DapperRepository.cs:48,57`). Consequently **every `IHasXmin` entity read through the SDK's Dapper read repository has `Xmin = 0`**, and any attach-based write of it fails with `DbUpdateConcurrencyException`. `IVersioned.Version` and SQL Server `RowVersion` are ordinary columns and *do* come back from `SELECT *`; only `xmin` has this problem, and `xmin` is the Postgres-preferred contract per `IHasXmin`'s own XML doc.

---

## 4. Shadow properties and the audit interceptor

### 4.1 The repo's audit fields are **not** shadow state

`ICreationAuditable.CreatedAt`, `IModificationAuditable.UpdatedAt`, `ISoftDeletable.IsDeleted`/`DeletedAt`, `I*AuditableBy<T>.CreatedBy`/`UpdatedBy` are all declared as real CLR `{ get; set; }` properties on the interfaces (`src/Data/Abstractions/`). So in this SDK they are ordinary mapped properties — Dapper can populate them, and `Update()` will happily null them. The probe confirmed `Update()` wrote `created_at = 0001-01-01`. Only the interceptor's explicit `IsModified = false` guard saves `CreatedAt` (`src/Data/EntityFrameworkCore/Audit/AuditInterceptor.cs:75`) — and it guards *only* `CreatedAt` and `CreatedBy`. Everything else is unprotected.

Consumer models may still declare genuine shadow properties, so §4.2 matters for the general contract.

### 4.2 Genuine shadow properties on an attached entity

- **Can they be set?** Yes: `entry.Property<string>("Note").CurrentValue = v`. Verified — emits `SET note = @p0`.
- ⚑ **`Update()` / `State = Modified` do NOT flag shadow properties.** Verified on EF 10.0.3: after `Update(g)`, `Note shadow=True isModified=False`, while every CLR property showed `isModified=True`. So shadow properties are accidentally *protected* from the `Update()` blast. **This is undocumented — do not rely on it as a guarantee.** It is reported here because it explains probe output that otherwise looks impossible, not as a design foundation.
- **A `CurrentValue` assignment marks the property modified and stays marked.** Re-assigning the same value does not clear it; clear it explicitly with `IsModified = false`. Verified.
- The EF docs flag exactly this hazard for the no-tracking-then-attach pattern: *"may also result in issues such as missing shadow property values, making it harder to get right."* A Dapper read has no way to carry them — there is no CLR property to land in.

### 4.3 Does the audit interceptor still fire on an attached entity?

**Yes, but only while `AutoDetectChangesEnabled` is `true`.** Both verified:

| setup | `updated_at` in the emitted SQL | row after |
|---|---|---|
| `Attach` + mutate, `AutoDetectChangesEnabled = true` | `SET name = @p0, updated_at = @p1` | stamped `2026-07-22` |
| `Attach` + `IsModified`, `AutoDetectChangesEnabled = false` | `SET name = @p0` only | **stayed `2020-01-01` — audit silently lost** |

The interceptor mutates the CLR property inside `SavingChanges`; that mutation only reaches the `SET` list because EF's own `DetectChanges` runs after the interceptor. Turn auto-detection off — a plausible optimization for a bulk pipeline — and `AuditInterceptor`, `SoftDeleteInterceptor`, and `AppDbContextBase.IncrementConcurrencyVersions` all stop working, with no error. Any pipeline that disables change detection must call `ChangeTracker.DetectChanges()` itself, or mark those properties explicitly.

`CreatedAt` protection does hold on the `Update()` path (`created_at` absent from the `SET` list, row kept `2020-01-01`) — but `description`, `price`, `category_id` were still destroyed in the same statement.

---

## 5. Owned types, value objects, navigations

| situation | behaviour on `Attach` + selective | behaviour on `Update()` | verified |
|---|---|---|---|
| owned reference is `null` (Dapper can't materialize nested objects) | owned entry not tracked; owned columns absent from `SET`; **columns preserved** | same — owned columns absent, **preserved** | yes |
| owned reference is `null` and the navigation is `IsRequired()` | `SaveChanges` succeeds, no exception, owned columns untouched | same | yes — EF 10.0.3 does **not** throw for a missing required owned dependent here |
| owned reference present but its properties left at CLR defaults (partial read) | untouched unless you flag them | ⚑ `SET dim_h = 0, dim_w = 0` — **owned columns destroyed** | yes |
| reference navigation `null` but the FK scalar was fetched | fine — FK is an ordinary property | fine | yes |
| FK scalar **not** fetched | untouched | ⚑ `SET category_id = NULL` — relationship destroyed, or an FK violation if the column is `NOT NULL` | yes |
| collection navigation empty/unfetched | nothing happens; existing child rows untouched | nothing happens | yes |
| collection navigation populated, child key at CLR default | ⚑ child tracked as `Added` → phantom `INSERT` | same | yes |

The pattern: **absent is safe, half-present is dangerous.** A `null` owned reference costs you nothing; an owned reference constructed with default values is worse than not constructing it at all. This directly contradicts the instinct to "always populate the value object so the model is valid".

---

## 6. Global query filters vs attach

**Query filters do not gate writes.** Verified end to end:

- row soft-deleted in the DB (`is_deleted = true`)
- `ctx.Products.FirstOrDefaultAsync(x => x.Id == id)` → `null` (filter applied: `WHERE NOT (p.is_deleted) AND p.id = @pid`)
- Dapper reads the same row fine (Dapper never sees the filter)
- `Attach` + mutate + `SaveChanges` → `UPDATE products SET name = @p0 WHERE id = @p1 AND xmin = @p2` → **succeeds**, row stays `is_deleted = true` with a new name

So a soft-deleted row can be read by Dapper, cached, attached, and written — a logically-deleted aggregate silently accepting mutations. Neither `ISoftDeletable`'s filter (`src/Data/EntityFrameworkCore/EntityModelConventions.cs:23`) nor the opt-in `ApplySoftDeleteFilter` (`src/Data/EntityFrameworkCore/SoftDelete/SoftDeleteModelBuilderExtensions.cs:26`) protects the write path. The read side has to apply the predicate itself.

Minor related note: both of those call the **unnamed** `HasQueryFilter`, which replaces rather than accumulates. EF 10 adds *named* query filters (`HasQueryFilter("SoftDeletionFilter", ...)` + `IgnoreQueryFilters(["SoftDeletionFilter"])`) which do accumulate and can be disabled selectively — relevant if the pipeline ever needs to read soft-deleted rows deliberately. Out of scope for question 1; worth a line in whichever doc owns filters.

---

## 7. Identity map — can it be pre-populated from a Dapper read?

**Yes, mechanically. `Attach` *is* the identity-map insert, and it costs zero SQL.** But three consequences follow, all verified:

1. **A later EF query for the same key returns the attached instance and discards the database values.** Verified: attach a partial entity, mutate `Name`, then run `ctx.Products.FirstOrDefaultAsync(x => x.Id == id)` → `ReferenceEquals(result, attached) == true`, `Description` still `null`, original value still `null`, state still `Modified`. The SQL *does* execute; the materialized values are thrown away. Docs are explicit: *"for a tracking query, if the entities returned are already tracked, then the tracked instances are used instead of creating instances from the data in the database."* **This is how a partial attach poisons every downstream EF read in the same scope.**
2. `Find`/`FindAsync` is served straight from the identity map — verified, **0 SQL statements**, returns the attached instance. Cheap, and equally poisoned.
3. Attaching a second instance with the same key throws `InvalidOperationException: The instance of entity type 'Product' cannot be tracked because another instance with the key value '{Id: ...}' is already being tracked.` So a request-scoped Dapper cache and the change tracker must be reconciled — two independent identity maps for the same keys will collide.

Escape hatches: `AsNoTracking()` returns a fresh, correct instance and does not conflict (verified — `ReferenceEquals == false`, `Description == "REAL-DESCRIPTION"`). `entry.Reload()` refills the tracked instance from the DB and resets state to `Unchanged` — but it **discards pending modifications** (verified: mutated `Name` reverted) and costs the round trip the whole design is trying to avoid.

---

## 8. Minimum column set for a safe attach

| write shape used downstream | minimum columns the Dapper read must fetch |
|---|---|
| `Attach` + mutate / `IsModified` / `CurrentValue` (**recommended**) | **PK** (all key columns; must be non-default when the key is store-generated) · **concurrency token** if the entity has one — for Postgres this means naming `xmin` explicitly · **every column the write will set**. Nothing else. |
| `Attach` + `CurrentValues.SetValues(incoming)` | the above **plus every column the incoming object carries** — any property present on the incoming DTO whose original was never fetched is a diff, hence a write |
| `Attach` + `OriginalValues.SetValues(cached)` | **every mapped column in the cached dictionary must also be present on the attached entity**; a key in the dictionary without a matching fetched value is a guaranteed overwrite |
| `Update()` / `State = Modified` | **every mapped column** of the entity, **plus every owned-type column** (as a fully constructed owned instance), **plus every FK scalar**, **plus every shadow property restored via `entry.Property(name).CurrentValue`**. This is "full materialization", and Dapper cannot reach shadow state or nested owned objects without a hand-written mapper. |

Read the last row as the answer to "does attach require a full entity?" — **only if you insist on `Update()`, and even a full Dapper row is not enough there** (shadow + owned). The cheap path is the narrow one.

---

## 9. The three viable write shapes

### 9.1 Partial attach + explicit modification — cheapest, most constrained

`Attach(partial)` → set the token's `CurrentValue` **and** `OriginalValue` if it wasn't fetched → mutate or flag only the intended columns → `SaveChanges`. One round trip, narrowest `UPDATE`, interceptors fire. Requires the caller to know which columns it is changing.

### 9.2 Full cached previous version + `CurrentValues.SetValues(incoming)` — the best fit for the owner's problem 3

The idea doc's problem 3 already wants the previous version cached for validation. If that cache entry is a *complete* row, this becomes the documented disconnected-update recipe and it is exact. Verified: attach the cached previous version, restore shadow state, `CurrentValues.SetValues(new { ... })` → `isModified` true for `Price` only → `UPDATE gadgets SET price = @p0 WHERE id = @p1 AND version = @p2`. Docs: *"SetValues will only mark as modified the properties that have different values to those in the tracked entity … And if nothing has changed, then no update will be sent at all."*

This makes the read-cache and the write path pay for each other: the same cached row that saves the validation re-read also produces a minimal diff-based `UPDATE`.

### 9.3 `ExecuteUpdateAsync` — no tracking at all

Verified: `Where(x => x.Id == id && x.Version == 7).ExecuteUpdateAsync(s => s.SetProperty(...))` → one `UPDATE ... WHERE g.id = @gid AND g.version = 7`, returns rows-affected, no change tracker involved, no attach, no partial-entity risk whatsoever. The concurrency check has to be written by hand into the `Where`, and the rows-affected result checked by hand.

⚑ It bypasses `SaveChanges` entirely — **no `AuditInterceptor`, no `SoftDeleteInterceptor`, no `IVersioned` increment, no outbox**. For this SDK that is a large hole, but it is the shape with the smallest correctness surface, and EF 10's non-expression `ExecuteUpdateAsync` lambda makes conditional setters easy. Worth keeping as the escape hatch for hot paths that own their own auditing.

---

## 10. What this forces on the pipeline design

1. **`IWriteRepository.UpdateAsync` cannot be the write path for a cached/projected entity.** `EfRepository.UpdateAsync` calls `Set.Update(entity)` (`:66`). Feeding it a Dapper-read entity destroys every column Dapper didn't select — and even a `SELECT *` read misses shadow properties, owned-type columns, and `xmin`. Either the pipeline routes around `IWriteRepository`, or the contract grows a change-set-aware overload. **This is a breaking-shaped decision and it belongs in the design, not in an implementation detail.**
2. **A cache entry must carry provenance: which columns it holds.** "Entity" is not a sufficient type for a cached read — a projection and a full row behave completely differently downstream. Either the cache is typed (`FullRow<T>` vs `Projection<T>`), or entries carry a column mask, and the attach layer refuses to serve a write it cannot satisfy. Without this, correctness depends on every caller remembering which SQL produced the object it was handed — which is exactly the coupling a pipeline is supposed to remove.
3. **The concurrency token is mandatory cargo, and Postgres makes it non-obvious.** Every read that may feed a write must name `xmin` explicitly; `SELECT *` will not do it. The cache entry must carry the token, and the attach step must set both `CurrentValue` and `OriginalValue`. A pipeline that gets this wrong on `IHasXmin` fails loudly (acceptable); on `IVersioned` it fails silently for freshly-inserted rows (not acceptable).
4. **Attach must be root-only.** A Dapper multi-map read that populates collection navigations turns any child with a default key into an `INSERT`. If graph attach is ever needed, it must go through `ChangeTracker.TrackGraph` with an explicit per-node policy, never bare `Attach`.
5. **The request-scoped read cache and the `DbContext` change tracker are two identity maps and they will collide.** One instance per key per context is enforced with an exception; a tracked partial entity silently wins over any later EF query for that key. Pick one: either the pipeline attaches into the scoped `DbContext` and treats the change tracker *as* the read cache (Dapper reads register there), or it never attaches during the read phase and defers all attaching to the write step. Running both maps independently is the failure mode, not a compromise.
6. **Never disable `AutoDetectChangesEnabled` in the pipeline** without calling `DetectChanges()` explicitly — it silently disarms `AuditInterceptor`, `SoftDeleteInterceptor`, and the `IVersioned` increment.
7. **The soft-delete filter protects reads only.** The pipeline's read layer must apply the `ISoftDeletable` predicate itself, because both Dapper and the attach/write path bypass it entirely.
8. **The performance goal is met, so optimize only for correctness.** `Attach` costs 0 SQL. There is no round-trip argument for or against any of the three shapes in §9 — they are all one statement. The whole decision is a safety decision.

---

## 11. Probe log — what was run and what it printed

Three programs, scratchpad only, nothing written into the repo. `/private/tmp/claude-501/.../scratchpad/efpg/` and `.../efpg2/`, run against `docker run postgres:16` on ports 55432/55433 (containers removed afterwards).

- **Probe 1** (`efpg`, sections A–H, 242 lines of output): `Attach` vs `Update` vs `State = Modified` vs `Property.IsModified` on a partial read · xmin fetched / unfetched / stale · shadow property under each API · `AuditInterceptor` with auto-detect on and off · owned type null / populated · reference nav + FK + collection nav · soft-delete filter vs attach · identity map (tracking query, double attach, `Find`, `AsNoTracking`, `Reload`).
- **Probe 2** (`efpg2`, P1–P9): per-property `IsModified` dump after `Update()` · store-generated PK at CLR default · graph attach with set/unset child keys · required owned dependent missing · `IVersioned` token silently matching zero · token injected as `OriginalValue` · `Attach` + `CurrentValues.SetValues` · `ExecuteUpdateAsync` · `Attach` SQL cost.
- **Probe 3** (`efpg2/Probe3.cs`, P10–P15): token injected as `OriginalValue` alone vs `Original`+`Current` · shadow `CurrentValue` re-assignment and `IsModified` reset · required owned null isolated from the token failure · `Update()` then un-flagging unfetched columns · `OriginalValues.SetValues` on a full vs a partial attach.

Representative output, the whole finding in four statements:

```
[A1 Update(p)]        UPDATE products SET category_id=@p0, created_at=@p1, deleted_at=@p2,
                        description=@p3, is_deleted=@p4, name=@p5, price=@p6, updated_at=@p7
                        WHERE id=@p8 AND xmin=@p9
  ROW AFTER: name=NEW-NAME | desc=<NULL> | price=0 | cat=<NULL> | created=0001-01-01

[A4 Attach + IsModified]  UPDATE products SET name = @p0 WHERE id = @p1 AND xmin = @p2
  ROW AFTER: name=NEW-NAME | desc=REAL-DESCRIPTION | price=99,50 | cat=222222 | created=2020-01-01

[B2 xmin NOT fetched (0)]  DbUpdateConcurrencyException: expected to affect 1 row(s), actually affected 0

[P5a IVersioned unfetched (0) vs row version 0]  SaveChanges OK  <-- silent match, no protection
```

Plus the standalone `psql` check that produced the `SELECT *` finding:

```
probe=# select * from t;         →   id | name
probe=# select xmin, * from t;   →   xmin | id | name
```

**Not verified** (stated as such rather than guessed): SQL Server `IRowVersioned` / `byte[] RowVersion` behaviour when the token is unfetched — no SQL Server instance was available. Everything else above was executed.

---

## 12. Sources

**Repo (read directly):**
- `src/Directory.Packages.props` — EF Core 10.0.3, Npgsql(.EFCore.PostgreSQL) 10.0.0, Dapper 2.1.66
- `src/Data/Abstractions/{IHasXmin,IRowVersioned,IVersioned,IAuditable,ICreationAuditable,IModificationAuditable,ISoftDeletable,IKeyedEntity}.cs`
- `src/Data/EntityFrameworkCore/EntityModelConventions.cs:23,26` · `AppDbContextBase.cs:48,55,59-63`
- `src/Data/EntityFrameworkCore/Postgres/PostgresModelBuilderExtensions.cs:20-25` · `SqlServer/SqlServerModelBuilderExtensions.cs:20-22`
- `src/Data/EntityFrameworkCore/Audit/AuditInterceptor.cs:49,66,71,75,100` · `SoftDelete/SoftDeleteInterceptor.cs`, `SoftDeleteModelBuilderExtensions.cs:26`
- `src/Data/EntityFrameworkCore/Repositories/EfRepository.cs:66` · `src/Data/Dapper/Repositories/DapperRepository.cs:48,57` · `src/Data/CqrsRepositoryServiceCollectionExtensions.cs`

**EF Core docs** (all pages last updated 2025-10-30 unless noted):
- [Explicitly Tracking Entities](https://learn.microsoft.com/en-us/ef/core/change-tracking/explicit-tracking) — `Attach`/`Update` state semantics, generated-vs-explicit keys, graph tracking, `TrackGraph`, the no-tracking-then-attach warning
- [Disconnected Entities](https://learn.microsoft.com/en-us/ef/core/saving/disconnected-entities) — `SetValues` diff semantics, `IsKeySet`, insert-or-update
- [Handling Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) — original-value comparison, `OriginalValues.SetValues(databaseValues)` refresh
- [Identity Resolution](https://learn.microsoft.com/en-us/ef/core/change-tracking/identity-resolution) — tracked instance wins over query results, duplicate-key exception, `Attach` + `OriginalValues.SetValues` recipe
- [Breaking changes in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes) (2025-10-09) — none affect this area
- [What's New in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew) (2025-10-02) — named query filters, non-expression `ExecuteUpdateAsync`
