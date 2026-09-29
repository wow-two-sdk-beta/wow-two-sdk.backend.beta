# Data session — test plan

*2026-07-25 · Test plan for [`data-session-architecture.md`](./data-session-architecture.md). Targets the 9 failure modes marked **"detectable? No"** in [`research/05`](./research/05-failure-modes.md) and defects D1–D6 in [`data-pipeline-verdict.md`](./data-pipeline-verdict.md). Design is not restated — only what gets asserted, how, and what breaks without it.*

> **ID collision warning.** The arch doc calls its build phases `0–3` **and** its load-bearing probes `P1–P4`. Here: phases are `P0`–`P3`, test ids are `P0-01`…, and the arch doc's probes are `PR1`–`PR4`.

---

## 1 · Suite decision

**Both — split by role. One new test *suite*, three deltas to the shipped *helper*.**

| What | Where | Why |
|---|---|---|
| `src/Data.Tests/` — **new** | `IsTestProject=true` · `IsPackable=false` | The arch doc makes it P0-non-negotiable; no `Data.Tests` exists. csproj cloned from `Migrations.Tests` + `CA1515` (public fixture types, per `Messaging.Tests`) |
| `RelationalTestDb` — **extend** | `src/Testing.Data/EntityFrameworkCore/` | Already a working provider-switchable Postgres/Respawn fixture. Three gaps block this vector — below |
| `DataSessionHarness` — **new, in the helper** | `src/Testing.Data/` | Products adopting `IDataSession` need the same recorder + census. `Testing.Messaging` precedent: the harness ships, the suite doesn't |

### `Testing.Data` deltas — each is a product gap, not test scaffolding

| # | Delta | Blocks without it |
|---|---|---|
| H1 | expose `DbConnection` / `NpgsqlDataSource` (today only `ConnectionString`, `RelationalTestDb.cs:25`) | the entire Dapper tier. On SQLite `ConnectionString` is `DataSource=:memory:` (`:50`) — a second connection opens a **different, empty** DB and every Dapper assertion passes vacuously |
| H2 | a DI-built context path (`AddEntityFrameworkCore` / `AddPostgresPersistence`) alongside `NewContext()` | `NewContext()` builds the options directly (`:34-43`) — it never runs the interceptor auto-wire loop (`EntityFrameworkCoreServiceCollectionExtensions.cs:66-68`) or the pooling branch (`:82-85`), so **D5 / F11 / F12 are invisible to it** |
| H3 | ctor overload taking a pre-built `PostgresFixture`, plus a `RelationalTestBase` mirroring `MigratorTestBase` | container is constructed inline (`:56`); house pattern is one container per class → ~40 containers for P2+P3. One collection fixture + `ResetAsync` per test instead |

`RelationalTestDb` has **zero consumers in-repo** — `Data.Tests` is its first. Expect H1–H3 plus the wrong `"call StartAsync first"` message (`:97,:99`) to shake out on day one.

### Test type per component

| Component | Type | One-line justification |
|---|---|---|
| P0 interceptor wiring + startup validation | **unit** (DI graph) | the claim is "which interceptors are on the resolved options" — no DB adds signal |
| P0 probes `PR1`/`PR2`/`PR4` | **integration** (Testcontainers PG) | savepoint events, pool reset and transaction collision have no in-memory analogue |
| P1 fragment SQL text | **unit** | pure string composition — cheap enough to be exhaustive over profile permutations |
| P1 predicate behaviour · xmin · converters | **integration** | a right-looking SQL string that queries wrong is exactly the D2/D3 shape; only a real PG catches it |
| P2 session · unit · lease · hooks | **integration** | savepoints, commit visibility and connection identity are the assertions; SQLite diverges on all three |
| P2 `DataUnitBehavior` | **unit** (fake session) | pure control flow — the only place in this vector a mock earns its keep |
| P3 write guards + attach | **integration** + emitted-SQL capture (`LogTo`) | the assertion is the `SET` column list, which exists only at the provider |
| 3 cross-phase flows | **E2E** (mediator → handler → session → repo → PG) | sanctioned update · rollback discards hooks · idempotency-before-commit each span every component |

**Pin Postgres for P1–P3.** Pass `DatabaseProvider.Postgres` through each fixture; SQLite has no `xmin`, no `xid`, no `FOR UPDATE`, and different savepoint semantics. See *Determinism* §D10.

---

## 2 · P0 — wiring, validation, harness

| id | what it asserts | type | what breaks if absent |
|---|---|---|---|
| P0-01 | an interceptor registered via `AddEfInterceptor` is present on the options resolved from `AddPostgresPersistence` | unit | **D5 / F12** — the flagship registration silently drops every hook, audit stamp and soft-delete. No warning; the concern just doesn't happen |
| P0-02 | interceptor order == DI registration order | unit | Rule A must observe entries before soft-delete flips state; without a pinned order the guard fires or doesn't by luck of registration |
| P0-03 | audit + soft-delete arrive **once**, via the loop, not also via the hardcoded `UseAuditInterceptor` | unit | double-registration → `UpdatedAt` stamped twice, soft-delete re-entered; both silent |
| P0-04 | `EnableRetryOnFailure` + `AddDataSession` → throws at `AddDataSession`/boot | unit | **F4** — the execution-strategy collision becomes a runtime `InvalidOperationException`, whose standard "fix" replays the whole delegate and double-applies every hook, `Version++` and audit stamp |
| P0-05 | retry **without** sessions still boots (escape hatch preserved) | unit | over-eager validation bricks every consumer that never opens a unit |
| P0-06 | `AutoDetectChangesEnabled=false` + audit/soft-delete/version interceptors → throws loudly | integration | research 01 §4.3 — all three interceptors silently stop; the row keeps a stale `updated_at` and nothing errors |
| P0-07 | the bulk-insert carve-out (Added-only tracker) does **not** throw | integration | the carve-out is unusable and bulk inserts are forced off the sanctioned path |
| P0-08 | **harness self-test** — `ResetAsync()` truncates; a row written by test N is gone in test N+1 | integration | `PostgresFixture.ResetAsync` returns early when the respawner is null (`:64-65`) — every isolation assumption in this plan is then void and the whole suite passes on leftover rows |
| P0-09 | **harness self-test** — a drain wait that times out reports what it actually saw | integration | `Testing.Messaging` precedent (`A_timed_out_wait_reports_what_it_actually_saw`): a bare `false` on a hook drain is undebuggable |
| P0-10 | `AddDapperConventions` casing is latched once per process and asserted, not assumed | unit | `SqlNaming` is static mutable (`SqlNaming.cs:13,16`) and the latch is `Interlocked` (`DapperServiceCollectionExtensions.cs:26-30`) — test **order** silently changes SQL, and xUnit parallelises across collections |

---

## 3 · P1 — read hardening (D2, D3)

| id | what it asserts | type | what breaks if absent |
|---|---|---|---|
| P1-01 | `EntityReadProfile` is projected from EF model metadata — soft-delete / tenant / `HasXmin` derived, never re-declared | integration | two registries drift; D2 and D3 return per-entity, forever, on every new model |
| P1-02 | `SqlRead.Filters<T>()` emits the soft-delete predicate for `ISoftDeletable`, nothing for a plain entity | unit | **F9** — Dapper serves rows EF hides; `repositories.md` claims the two repos are interchangeable |
| P1-03 | composed SQL actually hides a soft-deleted row against real PG | integration | a correct string that queries wrong (casing / parameter naming) — `SqlNaming` globals make this the likely failure, not the string |
| P1-04 | `Columns<T>()` names and aliases `xmin`; the read returns `Xmin != 0` | integration | **D3** — every `IHasXmin` attach-write throws `DbUpdateConcurrencyException` 100% of the time |
| P1-05 | xmin round-trips Dapper → attach → save: `UPDATE … WHERE xmin = @p` affects exactly 1 row | integration | P1-04 passes while the value lands in the wrong CLR property or the wrong type (`uint`↔`xid`) — selected but unusable |
| P1-06 | no ambient tenant + tenant-scoped entity + Dapper read → **throws** | integration | **D2, a security bug** — cross-tenant read, un-filtered by construction |
| P1-07 | with an ambient tenant, a foreign-tenant row is not returned | integration | fail-closed throws correctly but never actually filters |
| P1-08 | `DataReadOptions` tenant bypass returns the foreign row, and only on the explicit opt-in | integration | no escape hatch → ops/admin queries route around the seam entirely, re-opening D2 by hand |
| P1-09 | per-entity tenant-missing policy — an entity marked tenant-optional does not throw | unit + integration | fail-closed becomes global and the phase is un-shippable |
| P1-10 | `ForWrite()` outside a session unit → throws | integration | `FOR UPDATE` on an autocommit connection releases immediately — the pessimistic guarantee is a lie with no symptom |
| P1-11 | `RequireSessionTransaction` with no unit → throws; `Autonomous` with a unit open uses a **separate** connection and cannot see uncommitted rows | integration | the three consistency modes collapse into one and `Autonomous` silently joins |
| P1-12 | `FullRow<T>` is minted only by full-column reads; a projection read cannot produce one | unit | research 01 §10.2 — provenance is lost, a projection reaches the write path and clobbers columns |
| P1-13 | a Dapper read of a jsonb / converted column yields the same CLR value as the EF read | integration | **POC probe 6 / F10** — attach + save writes raw text or `null` over real jsonb. Silent data loss |
| P1-14 | **parity guard** — an EF `ValueConverter` with no Dapper counterpart is refused at profile build | integration | the only test here that scales with the codebase: without it F10 reopens on every new converted property, forever |
| P1-15 | a flattened owned type either materializes correctly or the profile refuses the entity | integration | F10's worst case — `DapperRepository`'s reflected column list maps owned columns to nothing → `null` written on save |

---

## 4 · P2 — session, unit, lease, hooks (D1, D6)

| id | what it asserts | type | what breaks if absent |
|---|---|---|---|
| P2-01 | `IDataSession` resolved from the **root** provider throws | unit | captive dependency → one session process-wide → cross-request transaction bleed |
| P2-02 | resolving the session opens no connection until `BeginAsync` | integration | every request pins a pooled connection for its whole lifetime → pool exhaustion under load, no error until it's total |
| P2-03 | begin → write → `CompleteAsync` → row visible from a second connection | integration | happy path; without it nothing else's negative result means anything |
| P2-04 | begin → write → dispose without complete → row **absent** | integration | **D6** — the rollback boundary doesn't exist and dispose silently commits |
| P2-05 | a Dapper write through the lease rolls back with the unit | integration | **D1 / F1 — the headline defect.** A Dapper write inside an EF transaction commits independently, silently, no log |
| P2-06 | a Dapper read through the lease sees the unit's uncommitted EF write | integration | D1's other half: the lease hands back a different pooled connection and read-your-own-writes fails invisibly |
| P2-07 | `OwnsConnection=false` — `DapperRepository`'s `await using` dispose is a **no-op**; a second Dapper call in the same unit still works | integration | the first Dapper call closes the session's connection and kills the transaction, or silently opens a new one (= D1 again) |
| P2-08 | no unit open → the lease falls back to the singleton `IDbConnectionFactory` and returns an *owned* connection | integration | today's behaviour breaks for every non-session consumer |
| P2-09 | `MigrationRunnerService` (singleton) still resolves the singleton factory, never a session | unit + integration | captive-dependency break at boot, or the migrator joins a request transaction |
| P2-10 | a custom `IDbConnectionFactory` that cannot join → throws/logs, never silently degrades | unit | D1 returns behind a config flag |
| P2-11 | nested `BeginAsync` issues a **savepoint**, not a second `BeginTransaction`; depth == 2 | integration | EF throws on a second `BeginTransaction` — nesting is unusable and jobs/sagas can't compose |
| P2-12 | inner `AbandonAsync` discards inner writes; outer stays committable and commits | integration | the savepoint boundary is decorative — abandon kills the whole transaction |
| P2-13 | only the **outermost** `CompleteAsync` issues COMMIT | integration | partial commit — an outer rollback can no longer undo inner work |
| P2-14 | depth counter is correct across begin/abandon/begin interleavings | integration | frames get tagged to the wrong depth → savepoint truncation under- or over-invalidates |
| P2-15 | nesting past a configured ceiling is refused or warned | integration | **F6** — held-open subtransactions past `PGPROC_MAX_CACHED_SUBXIDS = 64` trip a **cluster-wide** SLRU cliff from one request shape |
| P2-16 | `OnCommitted` frames run **after** COMMIT returns — the hook observes the row from a *second* connection | integration | **F2 / Q2** — the whole drain point. `SavedChanges` fires before commit, so hooks publish work that may still roll back |
| P2-17 | a single-command save inside a unit still drains | integration | under `AutoTransactionBehavior.WhenNeeded` EF's `BatchExecutor` skips the implicit transaction — `TransactionCommitted` never fires for **the most common case** |
| P2-18 | `OnRolledBack` runs on dispose-without-complete; `OnCommitted` does not | integration | rollback compensation never fires, or commit hooks fire on a rollback |
| P2-19 | rollback-to-savepoint discards **that frame's** intents; outer-frame intents survive and drain on the outer commit | integration | **F2** — the most common rollback shape in a layered design leaves stale intents that fire for work that never committed |
| P2-20 | `dedupKey` collapses into the **shallowest** frame: registered at depth 1 and depth 2, abandoning depth 2 leaves it live | integration | savepoint truncation **under-invalidates** — the dedup swallows the surviving registration and the cache keeps a dead value to TTL |
| P2-21 | no session active → `OnCommitted` runs inline immediately | integration | every write outside a unit (jobs, `PerWrite`) silently drops its hooks |
| P2-22 | a hook throwing mid-drain does not skip the remaining hooks; the transaction stays committed | integration | one bad adapter aborts every other hook, and the failure is misattributed to the commit |
| P2-23 | `DrainTimeout` exceeded → logs + increments the failed-intent counter; does not throw into the caller, does not roll back | integration | a hung hook converts a committed transaction into an apparent failure — the caller retries an already-committed command |
| P2-24 | the drain-timeout message carries a census of drained vs pending | integration | see P0-09; a timeout with no census cannot be debugged in CI |
| P2-25 | `DataUnitBehavior`: success → complete · failure result → abandon · exception → abandon + rethrow | unit | **a failure result silently commits** — the D1-class bug relocated to the mediator boundary |
| P2-26 | the behavior is never the owner — a scope that already opened a unit is joined, not re-wrapped | unit | every job pays a spurious savepoint, or the behavior commits the job's unit early |
| P2-27 | `PerWrite` (no unit) == today's behaviour: each repository write commits immediately | integration | the documented escape hatch regresses and existing consumers break with no migration note |
| P2-28 | `PerUnit` flushes per call **inside** the tx (visible to the session's own reads) but not to a second connection until complete; `Manual` flushes nothing until an explicit save | integration | the timing models are indistinguishable — either flush-visibility or isolation is wrong and both look fine |
| P2-29 | the `ConditionalWeakTable<DbTransaction, SessionCore>` bridge resolves the right session across **two concurrent scopes on pooled contexts** | integration, concurrent | **F11** — cross-request/cross-tenant session bleed. The worst failure in the vector and it only appears under concurrency |
| P2-30 | a **reused** `DbTransaction` instance never resolves a stale session | integration | pooling recycles instances; a stale bridge entry routes one request's hooks onto another's session |
| P2-31 | after `CompleteAsync` the session is `Committed`; a further write or complete throws | integration | reuse-after-commit silently opens a second transaction on the same scope |
| P2-32 | the whole P2 table runs **twice** — `AddDbContext` and `AddDbContextPool` | integration | **F11 verbatim**: "the same pipeline code is safe under one registration and leaks under the other" |
| P2-33 | an `IIdempotent` command whose unit rolls back leaves **no** cached SUCCESS | E2E | owner ruling 2 — a rolled-back command replays a fabricated success forever |
| P2-34 | a concurrency conflict inside a unit surfaces as a typed `AppError`, not a raw `DbUpdateConcurrencyException` | integration | the `DbExceptionMappingRule` seam is bypassed; the API returns 500 where it should return 409 |
| P2-35 | `IncrementConcurrencyVersions` + savepoint rollback + retry does **not** double-bump in-memory `Version` | integration | **F3** — a phantom concurrency exception with no concurrent writer, or a wrong token persisted. Unattributable in production |
| P2-36 | a statement error inside a unit without a savepoint leaves the unit poisoned **and says so** | integration | Postgres `25P02` — every later command in the unit fails with an error about the *previous* one; the real cause is lost |
| P2-37 | a cancelled `CancellationToken` mid-unit rolls back; no half-committed state | integration | request abort under load becomes a partial write |
| P2-38 | a dispose that throws during rollback does not mask the original exception | integration | the connection-died case reports the wrong error and the real failure never reaches logs |

---

## 5 · P3 — write guards (D4)

| id | what it asserts | type | what breaks if absent |
|---|---|---|---|
| P3-01 | sanctioned flow (`FindAsync` → attach → `CurrentValues.SetValues`) emits a **diff-only** `UPDATE` — assert the `SET` column list | integration + `LogTo` | **D4** — `Update()` flags every column and writes CLR defaults over real data, one statement, no error |
| P3-02 | **mutate-then-attach is unreachable or detected** — the seam never yields `Unchanged` on an entity with pending edits | integration | **POC probe 2 — the highest-value edge here.** `SaveChanges` writes nothing and returns success; the user's edit vanishes with no exception, no log, no row change |
| P3-03 | `Update()` / `State = Modified` on an untracked instance → **Rule A throws** | integration | D4's runtime backstop is gone; every hand-rolled `Update()` in every product clobbers silently |
| P3-04 | Rule A does **not** fire for a query-loaded-then-mutated entity (`FromQuery`) | integration | the guard is unusable — every normal update throws and it gets switched off |
| P3-05 | Rule A does **not** fire for `Added` entities | integration | inserts break |
| P3-06 | Rule A survives a `DbContextPool` **return + rent** (promoted `PR2`) | integration | the primary write guard is silently disarmed after the first request on a pooled context — armed in dev, off in prod |
| P3-07 | **Rule B** — concurrency token `OriginalValue` at CLR default on a tracked-for-write entity → throws | integration | research 01 §3.2: `IVersioned` at `0` **silently matches** any never-updated row — the only token case that fails successfully |
| P3-08 | Rule B ignores the **incoming** DTO's token (`Xmin=0` on incoming is normal) | integration | every legitimate update throws and the rule gets disabled wholesale |
| P3-09 | `IVersioned` seeds to **1** — a freshly inserted row is never `Version = 0` | integration | the silent-match window stays open on exactly the common case: first update after insert |
| P3-10 | `UpdateAsync` takes `FullRow<TEntity>`; non-`FullRow` provenance is rejected | unit | the provenance guarantee is advisory and a projection reaches the write path (P1-12's counterpart) |
| P3-11 | the `PreviousFrom = FullRow` (Dapper) path produces the same diff-only UPDATE and token behaviour as the `FindAsync` default | integration | the opt-in path diverges from the default and only production finds out |
| P3-12 | `FindAsync` on an identity-map hit issues **0 SQL** — assert command count | integration | the "0 round trips" claim is false: `GetByIdAsync` uses `FirstOrDefaultAsync`, which never consults the map |
| P3-13 | `FindAsync`'s map path does **not** return a foreign-tenant row | integration | **verified tenant leak** (research Q4): `ApplyTenantFilter` is a *query* filter — no query, no filter. Security |
| P3-14 | attach is root-only — a graph child at a default key never becomes a phantom `INSERT` | integration | research 01 §2 — a read turns into an insert; a multi-map Dapper read makes this routine |
| P3-15 | a soft-deleted row cannot be written through the sanctioned flow without an explicit opt-in | integration | research 01 §6 — a logically-deleted aggregate silently accepts mutations |
| P3-16 | the audit interceptor still stamps `UpdatedAt` on the attach path | integration | the sanctioned flow silently loses auditing where the tracked flow keeps it |
| P3-17 | `CreatedAt` / `CreatedBy` survive the sanctioned flow | integration | only these two are guarded (`AuditInterceptor.cs:75`); a regression there is invisible |
| P3-18 | a `null` owned reference preserves its columns; a defaults-constructed one is refused | integration | "absent is safe, half-present is dangerous" is a rule with no enforcement — F10's silent-null-write case |
| P3-19 | `UpdateAsync` on a missing row → typed NotFound, not `DbUpdateConcurrencyException` | integration | the documented breaking change isn't delivered; callers cannot tell 404 from 409 |
| P3-20 | pool exhaustion floor — one session + one `Autonomous` read = exactly **2** connections per request | integration | the default pool silently halves in capacity and surfaces only as timeouts under load |

---

## 6 · The 4 arch-doc probes → permanent tests

| Probe | Becomes | Where | Note |
|---|---|---|---|
| **PR1** — do `CreatedSavepoint` / `RolledBackToSavepoint` fire for EF's **automatic** per-`SaveChanges` savepoint on Npgsql? | **two** permanent cases: `P0-11` asserts the events fire (pins the version-dependent behaviour); `P0-12` asserts the **synthetic-frame fallback** produces identical depth-tagging when they don't | `Data.Tests/Session/SavepointEventsTests` | Both ship. PR1 is a *fork*, not a yes/no — a one-off script settles today's EF/Npgsql and pins nothing. The pair means an Npgsql upgrade that flips the answer fails a test instead of silently mis-tagging every hook frame |
| **PR2** — does a `ChangeTracker.Tracked` subscription survive `DbContextPool` reset? | `P3-06`, run under `AddDbContextPool` with an explicit **return + rent** between arm and assert; plus `P2-32`'s dual-registration run | `Data.Tests/Guards/RuleATests` | Never a standalone probe: the answer only matters as "Rule A is still armed on request N", which is the assertion itself |
| **PR3** — HybridCache tag invalidation cross-node (docs say no, `dotnet/extensions#7098` says yes) | a pinned-behaviour test in the future `Caching.Tests` (two `HybridCache` instances over one Redis container), **gated on the cache adapter** | not in `Data.Tests` | Out of P0–P3 scope. Until that suite exists the drain stays **key-based** (the conservative branch), so the answer cannot silently change behaviour. Record the probe verdict in the arch doc; do not build on it |
| **PR4** — outbox claim strategy opens its own transaction; collision with a session unit on the same context | `P2-39`: dispatcher scope + open unit on one context → asserts the chosen resolution (dispatcher skips units **or** the claim strategy joins). Plus `P2-40`: `PostgresSkipLockedOutboxClaimStrategy`'s ambient-transaction takeover does not fire while a unit is open | `Data.Tests/Session/OutboxCollisionTests` | The strategy already hand-rolls ~35 lines of transaction bridging with an `Interlocked` settle flag — the collision is a standing regression risk, not a one-time question |

---

## 7 · Edge cases

Ordered by *silence* — the ones with no exception, no log, no diagnostic come first.

| Edge | Why it is silent | Test |
|---|---|---|
| **mutate → `Attach`** makes the mutation the baseline; entity reads `Unchanged`; `SaveChanges` writes nothing | no exception, no log, `SaveChanges` returns normally. Only a data audit finds it | **P3-02** |
| **savepoint rollback discards hook frames** — inner save fails, outer commits | `TransactionRolledBack` never fires; a flat buffer drains intents for work that never committed | **P2-19**, **P2-20** |
| **`DbContextPool` reset disarms a `Tracked` subscription** | pooling resets the change tracker but not your fields; guard is armed on request 1, gone on request 2 | **P3-06** |
| **`OwnsConnection=false` dispose being a real dispose** | `DapperRepository`'s `await using` closes the session's connection; the transaction dies mid-unit | **P2-07** |
| **tenant fail-closed with no ambient tenant** | shipped EF filter is fail-open (`TenantModelBuilderExtensions.cs:41-43`: `noTenant ‖ matches`) — all rows returned, no error | **P1-06**, **P0-04**-adjacent asymmetry ruling |
| **jsonb `ValueConverter` not applied on a Dapper read** | raw text attaches as `Unchanged`; EF now believes the DB holds the raw value | **P1-13**, **P1-14** |
| **`FindAsync` map hit bypasses the tenant filter** | `Find` at `sql=0` returns a foreign-tenant row the identical LINQ query returns `null` for | **P3-13** |
| **`IVersioned` token at 0 matching a never-updated row** | write succeeds with zero concurrency protection — the only token case that fails *successfully* | **P3-07**, **P3-09** |
| **`xmin` round-trip Dapper → attach → save** | `SELECT *` omits it → `Xmin = 0` → conflict 100% of the time, or (worse) the right column in the wrong type | **P1-04**, **P1-05** |
| **process-global Dapper casing** | `AddDapperConventions`'s `Interlocked` latch makes the second configuration succeed and do nothing | **P0-10** |
| **nested `BeginAsync` depth** | wrong depth tag → truncation under/over-invalidates; and EF throws outright on a second `BeginTransaction` | **P2-11**, **P2-14** |
| **subtransaction depth past 64** | presents as a global DB slowdown, never as "that endpoint" | **P2-15** |
| **a hook throwing mid-drain** | one throw skips the rest; the transaction is already committed, so the failure looks like a commit failure | **P2-22** |
| **drain timeout** | a hung hook turns a committed command into an apparent failure the caller retries | **P2-23**, **P2-24** |
| **idempotency stored before commit** | a rolled-back command caches SUCCESS and replays it forever | **P2-33** |
| **concurrency conflict typing** | raw `DbUpdateConcurrencyException` escapes the mapping seam → 500 instead of 409 | **P2-34** |
| **`Version++` surviving a savepoint rollback** | CLR fields aren't transactional; retry bumps to DB+2 → phantom conflict with no concurrent writer | **P2-35** |
| **PG `25P02` poisoning after a statement error** | every later command reports an error about the *previous* one; the real cause is lost | **P2-36** |
| **`Attach` of a second instance with the same key** | `InvalidOperationException` — a read cache and the change tracker are two identity maps for one key set | **P3-14**-adjacent; assert the seam reconciles rather than collides |
| **graph child at a default PK** | `Attach` tracks the whole reachable graph → a phantom `INSERT` from a read | **P3-14** |
| **two `Autonomous` `ForWrite()` on one row** | a "concurrent writer" test that forgets `Autonomous` deadlocks against its own connection instead of conflicting | **P1-10**, **P1-11** (see §D8) |
| **cancellation mid-unit** | request abort under load becomes a partial write | **P2-37** |
| **dispose throwing during rollback** | masks the original exception; the real failure never reaches logs | **P2-38** |
| **Respawn ignores `migration_history` and does not reset sequences** | a test asserting on history or identity values reads the previous test's rows and passes | **P0-08** (assert the reset contract explicitly) |

---

## 8 · Not worth testing — say it out loud

| Skip | Why |
|---|---|
| EF's own `Attach` / `Update` / `SetValues` semantics | already **executed**, not recalled, in research 01 against the pinned EF 10.0.3 / Npgsql 10.0.0. Test *our guard*, never EF's behaviour — a re-run adds a container and zero signal |
| "`SELECT *` omits `xmin`" | a Postgres property verified with `psql`. `P1-04` asserts our column list; asserting Postgres asserts nothing about us |
| `RemoveByTagAsync` timestamp granularity (F8) | belongs to the deferred cache adapter. `PR3` records a verdict; a permanent test here would guard code that doesn't exist |
| cross-node L1 staleness (F7) · no XA between PG and Redis (F16) | **design limits, not bugs.** A test would assert a limitation — it can never fail and never changes behaviour. These get a documented-limitations section, not a test |
| read-replica routing (F14) | not expressible through the current seam. No code, no test |
| SQL Server `IRowVersioned` | unverified in research (no instance), Postgres is the target. State the gap; don't fake it |
| a second `DbContext` per session (F5) | explicitly deferred in the design |
| BenchmarkDotNet / the round-trip premise | research 01 §8 already concludes `Attach` costs 0 SQL and the decision is a **safety** decision, not a perf one. A number nobody will act on. Revisit with the cache adapter |
| mocked `IDataSession` unit tests | a mocked session proves nothing about savepoints, commit ordering or connection identity. The one exception is `DataUnitBehavior` (P2-25/26) — pure control flow |
| migrator engine behaviour | `Migrations.Tests` owns it. Only the captive-dependency wiring assertion (**P2-09**) is new here |
| the F6 cliff *as a load effect* | measuring cluster-wide SLRU contention is a benchmark. **P2-15** tests the depth guard; the cliff itself is a documented rationale |
| an architecture test that "no code calls `DbSet.Update`" | NetArchTest asserts type/reference shape, not call sites. This is an analyzer job or a code-review rule, not a passing green test |
| the SQLite provider path for P1–P3 | no `xmin`, no `xid`, no `FOR UPDATE`, different savepoint semantics. A green SQLite run here is worse than no run — it manufactures confidence (§D10) |

---

## 9 · Determinism hazards

| # | Hazard | Rule |
|---|---|---|
| **D1** | **`TimeProvider` + backoff deadlock.** Every SDK backoff sleeps on `TimeProvider`, so a test that fakes the clock **and** forces a retry/conflict **hangs instead of failing**. This bit the saga harness (`handoff.md` BATCH 11). In this vector's path: `OutboxDispatcher.cs:322` (`PR4`'s poll loop — one sweep, then parked forever) and `DefaultEventResiliencePipeline.cs:61` (any fault routed through messaging) | fake the clock only where nothing retries; otherwise drive the dispatcher explicitly or zero the interval. **Never wait on a sweep** |
| **D2** | audit + soft-delete register the clock with `TryAddSingleton(TimeProvider.System)` (`AuditServiceCollectionExtensions.cs:17`, `SoftDeleteServiceCollectionExtensions.cs:16`) | register a fake with `services.Replace(...)`, never `TryAdd` — precedent `SagaTestHarness.cs:63`. Otherwise whichever extension runs first wins and the interceptors keep stamping real time while the test asserts a fake one |
| **D3** | harness budgets on a faked clock never elapse | every wait/timeout budget in `DataSessionHarness` on `Stopwatch.GetElapsedTime` — precedent `MessagingRecorder.cs:55`, `Testing/Polling.cs:46`. The `DrainTimeout` test (P2-23) needs a **real** quiet window |
| **D4** | `EfMigrationsHostedService.cs:60` retries connect on the **real** clock | faking time does nothing here; a wrong connection string costs `ConnectRetryDelay × attempts` of wall time **per test**. Fail fast on the fixture's connection before the suite starts |
| **D5** | **process-global Dapper state.** `SqlNaming.ColumnCase`/`ParameterCase` static mutable (`SqlNaming.cs:13,16`) · `AddDapperConventions` latched once (`DapperServiceCollectionExtensions.cs:26-30`) · `MigratorHarness.NewServices()` sets `DefaultTypeMap.MatchNamesWithUnderscores = true` globally (`:162`) | pin the convention once in an assembly fixture, assert it (**P0-10**), and never vary it in-suite. xUnit parallelises across collections — this is a **race**, not just an order dependency. A case needing different casing needs its own process |
| **D6** | `xmin` changes on every update, including HOT updates | assert **before vs after**, never against a literal value |
| **D7** | Respawn truncates but keeps `migration_history` and does not reset sequences (`PostgresFixture.cs:52-57`); `ResetAsync` **silently returns early** when the respawner is null (`:64-65`) | **P0-08** guards the whole suite's isolation premise. Never assume reset ran |
| **D8** | a concurrency test needs **two real connections**. Under the lease everything joins one connection by design | the "other writer" must be `Autonomous` or a raw `NpgsqlConnection`; otherwise the test **deadlocks against itself** rather than conflicting — and a deadlock reads as a hang, not a failure |
| **D9** | house pattern is one container per test class (`Messaging.Tests` starts 7+ per run). P2+P3 is ~60 cases | one `ICollectionFixture` container + `ResetAsync` per test. Requires harness delta **H3** — `RelationalTestDb` constructs its container inline (`:56`) and cannot be handed one |
| **D10** | provider selection shared across fixtures would make parallel suites race | pass the provider to each `RelationalTestDb<TContext>` instance. On SQLite, `ConnectionString` is `DataSource=:memory:` (`:50`) — a Dapper connection opened from it hits a different, empty DB and the assertion passes on nothing |
| **D11** | `ConditionalWeakTable` release is GC-timed | do **not** force a collection to prove the bridge released. Assert the observable property instead: a reused `DbTransaction` never resolves a stale session (**P2-30**) |
| **D12** | `await using` scope exit vs assertion ordering | assert rollback-on-dispose from a **second** connection *after* the scope closes — asserting inside it reads the transaction's own uncommitted view and always passes |

---

## 10 · Conventions to match

- xUnit **2.9.2** (v2) — `IAsyncLifetime` returns `Task`; `xUnit1041` suppressed.
- `AwesomeAssertions` 9.0.0 · `Should().Be<T>()` for `Type` asserts (CA2263) · `BeOfType<T>()` for instances.
- Class names `sealed class <Concern>Tests`; methods `Sentence_in_snake_case_asserting_the_system`, subject = the SUT.
- `TreatWarningsAsErrors` is global → `<WarningsNotAsErrors>$(WarningsNotAsErrors);CA1707;CA2007;CA1062;CA1052;CA1819;CA1515;xUnit1041</WarningsNotAsErrors>`.
- `<FrameworkReference Include="Microsoft.AspNetCore.App" />` mandatory (NU1109 DI downgrade otherwise) · `<Using Include="Xunit" />`.
- Testcontainers 4.1.0, images pinned — `postgres:16-alpine`.
- Folder shape: `Tests/` + `Harness/` (the `Migrations.Tests` shape — this suite carries its own base class).
- Header comment naming the beta-forever exception, as every other suite does.
- Assertion lines carry a trailing `//` naming what the assertion proves.
