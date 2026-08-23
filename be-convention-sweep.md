# Backend convention sweep

*Last updated: 2026-08-22*

> Every code change the settled conventions imply, for this SDK and the products that consume it.
> Purpose — the conventions were rebuilt on 2026-08-17/18; this is the diff between what they say and what ships.
> Use case — pick a row, do it, tick it. Add a row whenever a convention lands that the code does not yet obey.

## Status

🔄 **53 of 87 rows open** — 31 shipped, 3 refuted — counted in the tables below on 2026-08-22. A row carrying ✅ is
shipped and one carrying ✗ is refuted; the rest are open. By lane — **27 product** (smart-qr · template · FE) ·
**18 SDK-only** · **3 SDK + products** · **3 conventions**. The backend conventions settled on **2026-08-19** — rows N16-N34 come from that pass and
are stable; N1-N15 predate it and should be re-read against the tree before acting.

Shipped rows deleted from the tables instead of ticked: `N26` `N51` `N59` `N62`. `N48` was counted as landed
and is not — its row stands open below. IDs above `N58` were assigned in chat on 2026-08-21 and never
written here; the ones recoverable from the handoff and the raised tasks are restored below — `N52` `N60`
`N69` `N74` `N77` `N81` `N82` `N85` `N86`. The remaining IDs in that range carry no recoverable text, so the
numbering has holes and a new row takes `N87` onward.

Source of truth for every rule cited here: `wow-two-ws/conventions/development/backend/dotnet/`,
recut on 2026-08-19 into `core/` (scope) and `shapes/` (deliverable). Paths below use the new tree.

**Measured 2026-08-19** against 969 declared SDK types, 1208 directive rules. Every row carrying a count
below was counted, not estimated; a row with no count was not mechanically checkable. Rows marked **[+]**
were added by that measurement, and the corrected counts on N22 · N26 · N34 replace single-symbol
estimates. Re-run the battery before and after any row: `engineering/planning/sweep.sh`.

| Check | Rule | SDK hits |
|---|---|---|
| fold suffixes | `core/mla/constructs/constructs.md` § *Folds* | 56 across 10 suffixes |
| banned suffixes | same § *Banned* | 6 across 4 suffixes |
| bare-noun `static class` | same, `:145` | 46 |
| bare `IEntity` on a concrete type | `entity-contracts.md:24` | 3 |
| `<remarks>` over the 5-line cap | `remarks.md:111` | 119 of 261 |
| `<para>` in a doc comment | `remarks.md` | 143 in 45 files |
| `<list>` markup | `remarks.md:116` | 12 files |
| severity glyph in a doc comment | `remarks.md:107` | 0 |
| `Options` bound from configuration | constructs § *`Settings` vs `Options`* | 0 (both hits third-party) |

---

## Naming and shape

| # | Change | Where | Source |
|---|---|---|---|
| N1 | `Dto` becomes edge-only — no `Dto` below the controller | products; smart-qr has 10 `Infrastructure` files touching one | conventions § *api messages* |
| ✅ N2 | ~~Introduce `Outcome`~~ — superseded by `N15`; the application shape is `{Noun}Model` | SDK + products | `core/mla/constructs/data/model.md` |
| N3 | Api request is verb-first `{Verb}{Noun}ApiRequest`; application request noun-first | products | `api-request.md` · `application-request.md` |
| N4 | Entity members are `{ get; set; }` + `required`; `init` everywhere else | products | `entity.md` · `data.md` |
| N5 | Data-access classes are `Repository`; `Query` / `Command` name folders, never classes | products | `repository.md` |
| N6 | Rename 11 pure static transforms to `Constants` · `Extensions` · `Mapper` | SDK | `core/mla/constructs/constructs.md` § *Folds* |
| ✅ N7 | resolved — it ships as `SqlNamingMapper`, and the battery's bare-noun static check no longer lists it | SDK | `core/mla/constructs/constructs.md:145` |
| ✅ N8 | `StyleSpecNormalizer` → `StyleSpecMapper` | SDK `src/Codes/Models/Style/` | decided, unshipped |
| N9 | Api-request ✅ examples are noun-first; the rule is verb-first | conventions + products | audit contradiction 5 |
| N10 | the rename is moot — `ValidationResult` no longer exists; `IValidator.Validate` returns `ValidationError?`, so the open half is wrapping it in `Result<T>`, which is `R4` | SDK `Foundation/Validation/` | `results.md` § *What returns a `Result`* |
| ✗ N11 | refuted by `N69` — a `Mapper` with no failure mode returns bare; the role never settles it | SDK + products | `results.md` § *What returns a `Result`* |
| ✅ N12 | `IErrorMessageResolver` → `…Mapper`; `Resolver` is a folded suffix | SDK `Foundation/Errors/` | `core/mla/constructs/constructs.md` § *Folds* |
| ✅ N13 | `IFieldErrorMessageResolver` → `…Mapper` | SDK `Foundation/Validation/` | same |
| N14 | Api layer takes domain folders — `Api/{Domain}/{Requests,Models}/` | products | `api.md` |
| N15 | Application shapes are `{Noun}Model`, one per shape, shared across operations | SDK + products | `core/mla/constructs/data/model.md` |
| N16 | A host `Add*` names a subject, never a layer — `AddApplicationServices()` and `AddPersistence()` banned | products | `shapes/service/platform/startup/host-configuration.md` § *Naming* |
| N17 | `AddPersistence()` → `AddPostgresDatabase()` in both hosts | smart-qr `*/Configurations/` | same |
| N18 | `AddApplicationServices()` dissolves — mediator to `AddMediator()`, `ICodeRepository` + `ISlugGenerator` to `AddCodes()` | smart-qr `SmartQr.Api/` | same |
| N19 | Drop the `Services` suffix — `AddCodeServices()` → `AddCodes()`, `AddRoutingServices()` likewise | smart-qr | same |
| N20 | One partial `HostConfiguration` across two files; no separate `HostConfigurationExtensions` class | products + template | `shapes/service/platform/startup/host-configuration.md` § *The partial split* |
| ✅ N21 | Add `IKeylessEntity : IEntity` and `ICompositeKeyEntity : IEntity` beside `IKeyedEntity<TId>` | SDK `Data/Abstractions/` | `core/mla/domains/persistence/entities/entity-contracts.md` § *Identity* |
| ✅ N22 | 3 concrete types implement bare `IEntity` — `IdentityUserRole` · `IdentityUserLogin` · `IdentityUserToken`, all composite join rows | SDK `Identity/Core/IdentityRelations.cs:7,55,73` | same |
| ✅ N23 | Bare `IEntity` never on a concrete type — audit every implementer once N21 lands | SDK + products | same |
| N24 | Ship a JSON serializer holding options in a type-keyed dictionary; products stop declaring a `static readonly JsonSerializerOptions` | SDK, lifted from `SmartQr.Common.Domain.Serialization.Json` | `smart-qr/be-sweep-handoff.md` § *JSON seams* |
| N25 | Re-test the `Json` keep-list row once N24 lands — the suffix's only claim was pinning options per type | conventions | `core/mla/constructs/constructs.md:75` |
| 🔄 N26 | 46 bare-noun `static class` types are none of the three forms — `SqlNaming` · `Geohash` · `Polling` · `QuietZone` · `CaseConverter` · … | SDK, whole tree | `core/mla/constructs/constructs.md:145` |
| ✅ N27 | `ColumnCase` / `ParameterCase` left the static as `SqlNamingOptions`, and every mapper method now takes its `CaseStyle` as an argument — `Options`, not `Settings`, because the caller supplies it in code | SDK `Data/Dapper/` | same + § *`Settings` vs `Options`* |
| ✅ N28 | Repoint every `SqlNaming.*` call site and the 14 `dapper.md` citations after N26 | SDK + products + conventions | `core/mla/domains/persistence/access/dapper/dapper.md:104-226` |
| ✅ N29 | `HostedService` folds into `BackgroundService` — rename `EfMigrationsHostedService<T>` and `DbUpHostedService` | SDK `Data/Migrations/` | `core/mla/constructs/constructs.md` § *Folds* |
| N30 | Background workers move out of `Services/` into `BackgroundServices/` | products; smart-qr `SmartQr.Redirect.Api/Infrastructure/Analytics/` | `core/mla/constructs/behavior/background-service.md` |
| N31 | `ContentEncodingExtensions` moves to a role folder; models into `Content/Models/` | smart-qr `SmartQr.Domain/Codes/Content/` | `core/mla/constructs/constructs.md` § *Location* |
| N32 | Validators move into a `Validators/` folder under their subdomain — matches the corrected rule | products | `core/mla/constructs/behavior/validator.md:12` |
| N33 | A nested sub-block in an api message is `{Noun}Dto`, never `{Noun}ApiRequest` | products + FE | `core/mla/domains/api/api-messages.md` § *Nested sub-blocks* |
| N34 | 28 fold hits + 5 banned names across 973 types — `Provider` 9 · `Resolver` 5 · `Observer` 4 · `Scheduler` 4 · `Source` 3 · `Keeper` 2 · `Profile` 1; banned `Strategy` 3 · `Manager` 1 · `Accessor` 1. Recounted 2026-08-22, down from 56 fold hits + 6 banned | SDK | `core/mla/constructs/constructs.md` § *Folds* · § *Banned* |
| N35 | `Api` project cuts by domain — `Api/{Domain}/{Controllers,Requests,Models}/` | products; smart-qr `SmartQr.Api/` | `shapes/service/architecture/architecture.md` § *Where a folder is created* |
| N36 | 7 smart-qr `*ApiRequest` sub-blocks become `*Dto` in `Api/{Domain}/Models/` — `Style` · `Gradient` · `GradientStop` · `LinearGradient` · `RadialGradient` · `Emoji` · `Logo` | smart-qr `SmartQr.Api/Requests/Codes/` | `core/mla/domains/api/api-messages.md` § *Nested sub-blocks* |
| N37 | `ControllerProblemExtensions.cs` moves off the project root into `Api/Extensions/` | smart-qr `SmartQr.Api/` | `core/mla/constructs/behavior/extensions.md` |
| N38 | **[+]** `// ── Section ──` banners become `#region` / `#endregion` — the divider rule was replaced by a region rule. Recounted 2026-08-22: 9 banners in 4 files, down from 28 in 8 | SDK | `core/lla/constructs/constructs.md` |
| ✅ N39 | **[+]** the lookup normalizer collapsed into `NamingExtensions.ToCanonical()` — the interface, its impl and its DI registration are gone, and `CaseStringExtensions` took the domain name the rule asks for | SDK `Foundation/Naming/` | `core/mla/constructs/behavior/extensions.md` § *Type name* |
| ✅ N40 | **[+]** `Options` gained a construct doc and a component doc, indexed from `data.md` · `components.md` · the keep-list | conventions | `core/mla/constructs/data/options.md` |
| ✅ N41 | **[+]** `patterns.md:119,148` now links the owner instead of restating the rule | conventions | `conventions.md` § *One owner per rule* |
| ✅ N42 | **[+]** 59 `*Options` classes became `sealed record` so `with` works — `TestAuthOptions` stays a class, it derives from a framework type | SDK | `core/mla/constructs/data/options.md` § *Declaration* |
| ✅ N43 | **[+]** 41 `{ get; init; }` members on `*Options` types became `{ get; set; }` for the delegate | SDK | same |
| ✅ N45 | **[+]** `{Capability}Extensions` and `{Primitive}Extensions` added to the naming rule; `CaseStringExtensions` → `CasingExtensions` | conventions + SDK | `core/mla/constructs/behavior/extensions.md` § *Type name* |
| ✅ N46 | **[+]** `DbUpOptions.ConnectionString` is `required` and arrives as an `AddDbUpRunner` parameter | SDK `Data/Migrations/DbUp/` | `core/mla/components/options.md` § *Registration* |
| N57 | **[+]** `AzureServiceBusOptions.ConnectionString` keeps its placeholder — the type stays on `AddOptions<T>()` for its `PostConfigure` chain, which cannot construct a `required` member | SDK `Messaging/AzureServiceBus/` | same |
| ✅ N58 | **[+]** the defaults rule lifted to `constructs.md` § *`Settings` vs `Options`* and both docs link it; location split by shape — a service uses the layer's `Settings/`, a library sits beside its `Add*` | conventions | `core/mla/constructs/constructs.md:112` |
| ✅ N47 | **[+]** 19 of 27 `AddOptions<T>()` registrations bought nothing and moved to `new T()` + `TryAddSingleton`; consumers dropped `IOptions<T>` and `.Value`. The 7 kept are the 5 transport→topology `PostConfigure` composers, `TopologyOptions`, and `WebhookOptions` for its `.Validate` delegates | SDK | `core/mla/components/options.md` § *Registration* |
| N48 | **[+]** 4 of 5 `ValidateOnStart()` calls carry no `.Validate` delegate and no `ValidateDataAnnotations`, so they validate nothing — `DbUp` · `EfMigrations` · `Database` ×2 | SDK | `core/mla/components/settings.md` § *Registration* |
| N49 | **[+]** one validation seam has to cover `Settings` and `Options` alike — a bound section and a delegate-filled instance validate through the same rules, so neither doc should ossify around `ValidateOnStart` | SDK + conventions | queued design |
| N50 | **[+]** SDK holds 1 `.Bind(` and 0 `reloadOnChange` — no value in it can reload, so `IOptionsMonitor` fires for nothing outside the 4 named-options sites | SDK | `core/mla/components/options.md` § *Registration* |
| ✅ N44 | **[+]** 7 broken relative links in the backend conventions repointed — 4 in `time.md`, 2 pre-move frontend paths, 1 repo path | conventions | mechanical |
| ✅ N86 | 12 types renamed onto the single `Repository` role, engine in the prefix — `EfUserRepository` · `DapperRepository` · `LocalFileBlobRepository` · `HybridCacheRepository` · `InMemoryTenantRepository` · `MemoryOtpRepository`, and `ICacheRepository` · `IBlobRepository` · `IOtpRepository` · `IIdempotencyRepository` on the contract side. `Storage` and `Cache` stop being roles, so swapping Postgres for Redis is a host-configuration line and no use case learns it happened | SDK | `core/mla/constructs/constructs.md:101` § *`Repository`* |
| ✅ N85 | folded into `N86` — ownership, contract shape and composition were each tried as the discriminator and each read an implementation fact, so none can carry a role | SDK | same |
| N74 | One registration mechanism for `Options` and `Settings`, validation first — `required` is decorative under `AddOptions<T>` because `Activator` bypasses it, so validation is the only enforcement | SDK | raised as `car-t-013`; rides with `N47` · `N69` |
| N77 | smart-qr's solution folders are lowercase; the rule is PascalCase | smart-qr | `shapes/service/architecture/architecture.md:57` |
| ✅ N87 | `MigrationScannerService` folded into `MigrationRunnerService` as a private `Scan()`, and `IMigrationScanner` deleted — source pluggability already lives in `IMigrationSource` (2 implementations plus a `sourceFactory` registration hook), while the scanner seam had 1 implementation, 1 consumer and no test fake, and no test called `Scan()` directly. DI registration dropped; Migrations suite 14/14 | SDK `Data/Migrations/Bespoke/` | `core/mla/constructs/constructs.md` § *Role and shape* |
| N88 | `CliRunner` is an `internal static partial class` with 8 public static methods and a `BuildProvider` that constructs a `ServiceProvider` — a bare-noun static is allowed only for `Constants` / `Extensions` / `Mapper`, so this becomes an instance service. `sweep.sh` greps `public static class` and missed it because the type is `internal` — widen the check | SDK `Data/Migrations/cli/CliRunner.cs:18` | `core/mla/constructs/constructs.md:145` |
| N89 | `MigrationsPathResolver` is an `internal static class` carrying a folded suffix — `Resolver` folds to `Mapper` (`N12` / `N13`), and the path lookup is meant to be overridable, which a static cannot be. Becomes an instance service behind a contract | SDK `Data/Migrations/cli/MigrationsPathResolver.cs:4` | `core/mla/constructs/constructs.md` § *Folds* + `:145` |
| ✅ N90 | `IErrorMessageMapper` and `IFieldErrorMessageMapper` moved to `Web/ErrorMapping/`, and the DI half of `ExceptionMapping.cs` split into its own `*ServiceCollectionExtensions.cs` — `Foundation/Errors/` core is BCL-only, so the migrator CLI's exclude narrowed from 2 named files to the `*ServiceCollectionExtensions.cs` pattern. Originally: `IErrorMessageMapper` imports `Microsoft.AspNetCore.Http` and `ExceptionMapping` imports the DI container, both from `Foundation/Errors/` — a Foundation primitive must not know about web or hosting. Surfaced when the web-free migrator CLI had to exclude exactly these 2 files by name to link the `Result` carrier; move them to the web layer and the exclude list goes away | SDK `Foundation/Errors/` + `Data/Migrations/cli/*.csproj` | `core/mla/constructs/constructs.md` § *Role and shape* |

---

## Result pattern

| # | Change | Where | Source |
|---|---|---|---|
| ✗ R1 | refuted by `N69` — the failure mode decides, so "every behavior component" over-reaches | SDK + products | same |
| ✗ R2 | refuted by `N69` — `Extensions` is not exempt either; `TryX` + `bool` stays a shape choice, not an exemption | SDK + products | same |
| ✅ R3 | superseded by `N69` — the failure mode decides, not the role | SDK + products | `results.md` § *What returns a `Result`* |
| R4 | A `Validator` returns a `Result` — rule failures are the success payload | SDK + products | settled |
| R5 | `AddMediatorExceptionToResultBehavior` has zero call sites; wire it or delete it | SDK `Mediator/` | `ideas/exceptions-analysis.md` |
| R6 | 11 smart-qr handlers hand-roll try/catch, preempting the DB error mapping | smart-qr | same |
| ✅ R7 | resolved — `results.md` § *Carriers* gives each a lane: `Result<T>` everywhere, `AppResult<TSuccess>` for mediator handlers ↔ controllers, and an inner `Result<T>` maps up in the handler | SDK | `results.md:13` § *Carriers* |
| R8 | Add `Result<TSuccess, TFailure>` — a caller cannot branch exhaustively on `AppError` today | SDK `Foundation/Results/` | `shapes/service/platform/responses/results.md` § *Typed failure* |
| ✅ N81 | **12 domain throws become `Result`** — 10 landed: `ClaimCheckPayloadRepository` (4, plus 2 `throw Missing(...)` helpers the `throw new` scan had missed) and `MigrationRunnerService` (5, scanner rows included). `Result<T>` gained `IsFailure(out error, out value)` along the way — the carrier has `Map` but no `Bind`, so propagating a failure through an async operation otherwise cost a cast per hop. Startup bridges back to a throw at `PostgresPersistenceStartupExtensions`, since a host has no result channel before its pipeline exists. `OAuth2TokenRepository.GetAccessTokenAsync` returns `Result<string>` and the `DelegatingHandler` bridges back, because the `HttpClient` pipeline reads only exceptions. **Two carve-outs close the row.** `DbUpBackgroundService:59` is an `IHostedService.StartAsync` — a host has no result channel before its pipeline exists, the same reason the Postgres startup path bridges back to a throw. `CloudEventsMessageSerializer:154` is blocked by the carrier, not by taste: `IMessageSerializer.Deserialize` returns `object?` and `Result<T>` constrains `T : notnull`, so converting it needs a redesigned serializer contract across every implementation and transport adapter — raised as `N91`, one type at a time. ✅ `ClaimCheckPayloadRepository` landed 2026-08-22 — `ReadAsync` returns `Result<byte[]>`, `Confine` returns `Result<string>`, `Missing` returns an `AppError`, and the filter bridges back at `ClaimCheck.cs:461` with `.Match(body => body, error => throw new ClaimCheckPayloadException(error.Message))`; 6 failure exits, not 4, because two were `throw Missing(...)` rather than `throw new`. Messaging suite 96/96. Scope filter — throws inside role-suffixed types only (`Service` · `Repository` · `Mapper` · `Adapter` · `Validator` · `Serializer`); 29 counted, 9 excluded as config-at-boot or programmer errors (`N82`), 7 excluded as argument guards and `NotSupportedException`. The 13, in landing order: `ClaimCheckPayloadRepository` 4 (`ClaimCheck.cs:223,242,291,295`) · `MigrationRunnerService` 5 (`:150,227,248` plus the 2 folded in by `N87`) · `DbUpBackgroundService` 1 (`:59`) · `OAuth2TokenRepository` 1 (`:102`) · `CloudEventsMessageSerializer` 1 (`:154`). `FluentValidationAdapter:52` is carved out — `ValidateAndThrow` is the declared throw half of the bridge, paired with `Validate` returning `ValidationError?`, so throwing is its contract rather than a missing `Result`. Re-scanned 2026-08-22 for throw-helpers (`throw Helper(...)`, the shape that hid two exits in the claim-check repository): none in the remaining types, so these counts stand. Each conversion changes a contract and every caller, so they land one type at a time with the suite green between them. Rides along: `ClaimCheckPayloadRepository`'s ctor parameter is still named `blobStorage` after `N86` | SDK | `results.md` § *What returns a `Result`* |
| ✅ N82 | the 9 config-at-boot and programmer-error throws stay exceptions and guards stay guards — the framework's own seams only catch what is thrown. The 9: `DbUpBackgroundService:33,38,41` · `MigrationRunnerService:129,181` (both gated on `MigrationOptions.AllowRollback`) · `OAuth2TokenRepository:66,71` · `ConfigurationMapper:42` · `EfInterceptorWiringValidator:109` | SDK | same |
| ✅ N69 | the failure mode decides, not the role — an operation with a failure mode wraps, one that cannot fail by construction returns bare. Supersedes `R3`, and opened by iteration 8 of the taxonomy analysis | SDK + products | same |
| ✅ N60 | `AppErrorProblemDetailsFactory` is a static class, so it cannot be swapped or decorated and every branch lands in one method — creation moves behind an interface | SDK | raised as `car-t-012` |
| N91 | `IMessageSerializer.Deserialize` returns `object?`, which `Result<T>` cannot carry under its `T : notnull` constraint — decide whether the seam returns `Result<object>` plus an explicit empty case, or stays throw-based as a documented boundary. Blocks the last `N81` throw (`CloudEventsMessageSerializer:154`) | SDK `Messaging/Serialization/` | `shapes/service/platform/responses/results.md` § *What returns a `Result`* |

---

## Documentation

| # | Change | Where | Source |
|---|---|---|---|
| D1 | 295 `<remarks>` blocks over the 5-line cap | products | remarks standardization |
| D2 | 147 `<remarks>` restating their own summary | products | same |
| D3 | 45 `.cs` files use `<para>` — recast as compact bullets | SDK + products | `remarks.md` |
| D4 | Severity glyphs out of doc blocks — `⚠`, `❗`, `NOTE:` | products | `remarks.md` |
| D5 | Apply the falsifiability test to every remaining type summary | products | v0.9 Iteration 8 |
| D6 | **[+]** 119 of 261 SDK `<remarks>` blocks exceed the 5-line cap — worst is 41 lines | SDK, `Messaging/` and `Testing/` carry most | `remarks.md:111` |
| D7 | **[+]** 12 SDK files use `<list>` markup, banned outright | SDK | `remarks.md:116` |
| N52 | 18 files carry `<example>`, banned outright | SDK | `core/lla/notation/documentation/inline.md:35` |

---

## Correctness

| # | Change | Where | Source |
|---|---|---|---|
| C1 | Raw exception messages reach the client through ProblemDetails `detail` | smart-qr | `ideas/exceptions-analysis.md` |
| C2 | A Postgres `23505` renders 500 instead of 409 — the catch preempts the mapper | smart-qr | same |
| C3 | Trace id survives the request chain but never reaches a log line | SDK | `ideas/logging-analysis.md` |
| C4 | `Logging:LogLevel` in `appsettings.json` is inert under Serilog | smart-qr | same |
| C5 | `UseOwaspSecureHeaders` hard-codes COOP/COEP with no opt-out | SDK | `docs/backend-inventory.md` |
| C6 | 4 null-forgiving sites are genuine lies with a named failure mode | SDK | `ideas/nullability-and-fixtures-analysis.md` |
| C7 | smart-qr has no `Directory.Build.props`, so nullable warnings never surface | smart-qr | same |

---

## Phantom symbols

Cited in conventions, absent from source. Either build them or cut the citation.

- ~~`AddEnvironmentOverrides` · `AddIntegrations` · `AddPipelines` · `AddSchedulers` · `AddObservers`~~ —
  **cut 2026-08-19.** The table that held them was the stale horizontal model; the doc now documents only
  methods a host declares. `hosted-service.md:82` still cites `AddSchedulers()` — repoint it to `AddCodes()`-style
  domain wiring.
- `IErrorMessageMapper` / `IFieldErrorMessageMapper` — the docs are **right** and the source is wrong:
  it ships as `…Resolver`, and `Resolver` is a folded suffix. Rows N12 and N13 rename the source.

---

## Open

- ~~whether the 5 phantom `Add*` methods were a plan or a mistake~~ — cut, they were the horizontal model's residue
