# Historical backend convention sweep

## Verified closure — 2026-09-16

C17 is complete. CI now creates one release commit before restore/build/test/pack, embeds that revision in all
seven package pairs, verifies assets/family links/dependency isolation and records a retained manifest. It publishes
before pushing the tested commit/tag, then verifies all seven NuGet versions and the remote tag. Local release tests
passed 402 with one skipped; all package checks passed.
[Verification](../../../system/sessions/backend-beta-build/c17-release-pipeline-verification.md).

C16 is complete. The shared packability default preserves explicit project values: all seven test projects now
evaluate non-packable while the seven release projects remain packable. `global.json` pins SDK `10.0.300` with
`latestPatch`, and CI reads the same file. All 14 projects were evaluated; restore and solution build passed.
[Verification](../../../system/sessions/backend-beta-build/c16-build-packaging-verification.md).

C15 is complete. `UseApiDefaults` routes before endpoint-metadata consumers and accepts an explicit identity
middleware callback before rate limiting and output caching. Response compression wraps response writers.
A real TestServer host verified route metadata, authorization, identity-based limiter/cache policies and gzip;
all 47 Web tests passed.
[Verification](../../../system/sessions/backend-beta-build/c15-api-defaults-pipeline-verification.md).

N115 is complete. Vulnerable direct dependencies were upgraded, vulnerable transitive dependencies were pinned,
the redundant HTTP and obsolete SourceLink references were removed, and NuGet advisories again fail the build.
All 14 projects passed the live vulnerability audit and solution build; 399 tests passed with one Kafka integration
test explicitly skipped. [Verification](../../../system/sessions/backend-beta-build/n115-dependency-remediation-verification.md).

N111 is complete. Rule-free code options are direct singleton instances; constrained options use the shared
startup-validation recipe; the five broker topology `PostConfigure` calls retain their required options pipeline.
SDK services consume direct records. The delayed-retry callback no longer mutates `IServiceCollection` during
resolution. The SDK builds; 3 focused registration, 8 identity, 17 migration and 4 Dapper tests passed.
[Verification](../../../system/sessions/backend-beta-build/n111-options-registration-verification.md).

N108 is complete. All public concrete role summaries use their mandated starter; public interfaces start with
`Defines`, and documented fixed-value fields start with `Holds`. Hash-chain docs now state the implemented boundary:
one canonicalizer validates one chain from sequence 1 and an empty prior hash; mixed-version segments are unsupported.
The main SDK project builds with zero errors.
[Verification](../../../system/sessions/backend-beta-build/n108-summary-conformance-verification.md).

N110 is complete. All authored production `//` comments are single-line; explanatory runs were reduced to the
behavior or invariant that changes maintenance. Generated assembly metadata is the only multi-line run left and
is excluded by convention. The main SDK project builds with no errors.
[Verification](../../../system/sessions/backend-beta-build/n110-inline-comment-verification.md).

N26 is complete by current-source inventory. All 247 static class declarations use a permitted role suffix;
the only bare noun is the allowed non-generic `SagaTestHarness` companion beside `SagaTestHarness<TState>`.
No stale `SqlNaming`, `Geohash`, `Polling`, `QuietZone` or `CaseConverter` type remains.
[Verification](../../../system/sessions/backend-beta-build/n26-static-class-conformance-verification.md).

C27 is complete by source inventory. The SDK declares no `*ValueObject`; collection-bearing records are DTOs,
options or mutable operational state. Their generated record equality is not presented as a value-object contract,
so no custom equality or hashing was added and entity identity remains unchanged.
[Verification](../../../system/sessions/backend-beta-build/c27-value-object-equality-verification.md).

C28 is complete by inventory. The SDK declares no top-level HTTP `*ApiRequest` and no `ToCommand` / `ToQuery`
request mapping. Its three `*Request` types are a code-render model and two internal messaging state objects, not
HTTP bodies. No unused request model or mapping companion was added.
[Verification](../../../system/sessions/backend-beta-build/c28-api-request-mapping-verification.md).

N94 is complete. `ICurrentUser` and `IAuditCurrentUserService` are unified as `ICurrentUserService`, backed by
singleton `CookieCurrentUserService`. It reads `IHttpContextAccessor` on every property access, retains no request
principal and reports anonymous outside HTTP, so the singleton audit and soft-delete interceptors read the current
user on each save without a scoped dependency. Identity.Tests passed 8 and Data.Tests passed 23.
[Verification](../../../system/sessions/backend-beta-build/n94-current-user-service-verification.md).

N102 is complete. `ICryptoCore` / `CryptoCore` became `IValueCipher` / `ValueCipher`; registration, security
contracts and references use the Cipher role. `AesGcmCipher` remains the wrapped primitive. Foundation.Tests
passed all 123 tests, including DI resolution. SecretsVault adoption remains release-dependent consumer work.
[Verification](../../../system/sessions/backend-beta-build/n102-value-cipher-verification.md).

N103 is complete. Sending an unregistered event type now throws as incomplete local composition; an absent
content-type header retains the legacy default, while an explicit unregistered content type fails into the
transport's unparseable-message path. Unknown inbound type tokens remain unresolved untrusted input. The other
reported misses are predicates: destination membership, optional event handling and reply correlation races;
`ConsumedMessageTypeRegistry` exposes no lookup. Messaging.Tests passed 113 with 1 skipped.
[Verification](../../../system/sessions/backend-beta-build/n103-registry-failure-verification.md).

C26 is complete. The SDK pins and exports FastCloner 3.5.6 for direct explicit deep cloning of owned,
detached data graphs. Contract tests verify runtime types, private/init/get-only state, cycles, shared aliases,
original isolation, comparer preservation, usable cloned hash keys and preserved entity IDs. Live resources,
EF contexts/proxies and NativeAOT remain outside the claim. Foundation.Tests passed all 123 tests.
[Verification](../../../system/sessions/backend-beta-build/c26-fastcloner-integration-verification.md).

N114 is complete. `EfRepository` and `EfUserRepository` save tracked instances without `DbSet.Update`,
preserving original concurrency values and property-level changes. Both reject a detached replacement while a
same-key instance is tracked; detached full-state updates remain explicit when no duplicate is tracked. Current
SDK source has no entity navigation collection or entity-object hash lookup to repair; the retained 20-assertion
EF experiment verifies reference navigation membership and stable-key guidance. Data tests passed 3 and Identity
tests passed all 7. [Verification](../../../system/sessions/backend-beta-build/n114-tracked-write-verification.md).

C29 is complete. Immutable stored `JsonSerializerOptions` can be registered and resolved by an
application-owned string key, with overloads for a pinned instance or SDK factory modifiers. Unknown keys and
mutable options fail explicitly. Direct options remain supported; EF conversion and comparison receive the same
instance. The ForeverPin handoff now removes its per-type wrappers after repin without adding an options holder.
Foundation.Tests passed all 119 tests.
[Verification](../../../system/sessions/backend-beta-build/c29-stored-json-profiles-verification.md).

C14 is complete. `JsonOptionsConstants.Default` and `AddControllersWithSdkJson` now enforce camelCase string
enums with numeric values disabled and ISO 8601 `TimeSpan` values. Controller-preset coverage verifies
dictionary casing, null omission, scalar round trips, known flags, and rejection of numeric/undefined enum
values and unknown flag bits. Stored JSON remains an independent preset. Web.Tests passed all 44 tests.
[Verification](../../../system/sessions/backend-beta-build/c14-http-serialization-verification.md).

C21 is complete. The mediator interceptor aggregates every registered SDK validator's field failures in
registration order and throws one SDK `ValidationException`; FluentValidation custom error codes remain exact.
Validation docs now reserve the interceptor for target-free requests and put target-bearing validation after
target resolution and ownership checks. SDK source contains no nested request-handler dispatch to replace.
`GeoBoundingBox` documents its only exceptional constructor data contract and preserves it through get-only
copy/deserialization paths. Foundation.Tests passed 115 and Mediator.Tests passed 68.
[Verification](../../../system/sessions/backend-beta-build/c21-validation-conformance-verification.md).

N104 is complete. `IRequestClient.GetResponseAsync` returns `Result<TResponse>`; timeouts return `OperationTimeout`
and an inconsistent reply body returns `SerializationFailed`. The obsolete request exception types are removed.
`IMigrationBroker.Read` now returns migration-source failures, and every runner operation propagates them from `Scan()`.
Messaging.Tests passed 111 with 1 skipped; Migrations.Tests passed all 17, including three broker regressions.
[Verification](../../../system/sessions/backend-beta-build/n104-result-contract-verification.md).

N60 is complete. ProblemDetails creation is behind `IAppErrorProblemDetailsFactory`; the default factory and interface
live under `ExceptionHandling/Factories/`. Exception handlers and the MVC filter consume the seam, while registration
preserves an app implementation registered first. The SDK build passed and Web.Tests passed all 39 tests.
[Verification](../../../system/sessions/backend-beta-build/n60-problem-details-factory-verification.md).

N101 is complete. The 12 provider transport implementations moved into owning `Transports/` folders and namespaces;
the saga delivery wrapper became `EventSagaPublisherService` because it guards routes and delegates to `IEventBus`.
GeoJSON now preserves string/number ID kinds, validates root/member shape and rejects unsupported position arity while
documenting the model's explicit subset. Google authentication observes caller cancellation and documents the provider
API's non-cancelable certificate-refresh limit. The SDK build passed; Messaging.Tests passed 111 with 1 skipped,
Foundation.Tests passed 114, and Identity.Tests passed 6.
[Verification](../../../system/sessions/backend-beta-build/n101-conformance-verification.md).

## Verified closure after cleanup — 2026-09-15

N100 is complete. Its final Exporter slice documents provider-specific schema, empty output, stream ownership,
position/truncation, partial failures and buffering/cancellation boundaries. CSV preserves caller cancellation
and writes nothing for pre-canceled calls; unrelated writer failures keep their failure path. The SDK core
compiled with 17 warnings and 0 errors. Runtime verification inspected 34 scenarios; seven cancellation
regression assertions passed. [Exporter verification](../../../system/sessions/backend-beta-build/exporter-conformance-verification.md).
General member documentation remains N108 work; N101 owns the separate Transport and remaining contract work.

The Parser portion of N100 is complete: all 14 declarations are in owning `Parsers/` folders, shared
declaration/input contracts are reconciled, and Cron occurrence calculation belongs to the returned expression.
Compilation and all 17 existing VTT cases passed against the current SDK through a temporary consumer-test import.
Other formats remain compilation/source verified. The Exporter closure above completes N100.
[Parser verification](../../../system/sessions/backend-beta-build/parser-conformance-verification.md).

N91 was reopened by the live-source recheck and fixed in the same turn. CloudEvents decoding validates
the complete JSON document, returns SerializationFailed for malformed JSON/base64 and invalid base64
payload shapes, and preserves the null bodyType programmer guard. It remains a payload decoder and does
not require or semantically validate inbound CloudEvents context attributes. The serialization guide now
describes the actual Result<object> contract. Messaging.Tests compiled; all 37 serializer tests passed,
including 15 new regressions. [Verification](../../../system/sessions/backend-beta-build/cloudevents-decoder-verification.md).

The three recorder Tracker role-folder moves also compiled, with all 13 existing messaging/saga harness
tests passed. [Verification](../../../system/sessions/backend-beta-build/tracker-placement-verification.md).

## Pre-cleanup snapshot

Archived 2026-09-15 during handoff cleanup. The [active sweep](be-convention-sweep.md)
owns current work. This snapshot preserves superseded statuses, measurements and discussion;
it is not a fresh source or runtime certification. Historical N91 completion is superseded by
the malformed CloudEvents finding in the active sweep.

---

# Backend convention sweep

*Last updated: 2026-09-15*

> Every code change the settled conventions imply, for this SDK and the products that consume it.
> Purpose — the conventions were rebuilt on 2026-08-17/18; this is the diff between what they say and what ships.
> Use case — pick a row, do it, tick it. Add a row whenever a convention lands that the code does not yet obey.

## Status

🔄 **28 of 107 rows open** — 73 marked complete, 6 refuted. The convention repair pass added `C14`–`C25` and the earlier entity decision added `N114` on 2026-09-10; deep copying and value-object equality added `C26`–`C27` on 2026-09-12; request mapping and shared serializer completion added `C28`–`C29` on 2026-09-13. `N25` closed as a convention-only change. `N60` remains reopened against source. The 31 product rows moved to `smart-qr-poc/smartqr-be-update.md` on 2026-08-25. Completion markers are not a fresh source/build verification. A row carrying ✅ is
historically completed and one carrying ✗ is refuted; the rest are open. Product adoption is tracked separately.
Every row is rechecked against the current convention owner when resumed; an old completion date is not
an exemption from a corrected rule.

Preserve existing row IDs, including gaps from historical records. `N26` is open and `N48` is marked
completed in the tables. The highest assigned naming-row ID is `N114`; new naming rows start at `N115`.
New work extends a matching existing row instead of assigning a second ID for the same obligation.
Historical IDs `N94`–`N98` each occur twice; references must include the row's subject. Counts measure table
rows, not distinct IDs. Preserve these records rather than silently renumbering prior decisions.

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

**Re-swept 2026-09-03** — a read-only agent pass over 1000 declarations, findings landed as `N95`–`N103`. Clean on
re-measure: `Settings` vs `Options` origin (every `.Bind(` / `Configure(` site, 0 mismatches) · `Factory` vs `Mapper`
(7 audited, 0) · two role words stacked in one name (0 — every hit is `*ServiceCollectionExtensions` or a
third-party noun) · singular role folders (0) · `Settings/` folders (0) · misplaced `static readonly` values
(27 sampled, each a private computation cache). `sweep.sh` check 9 now lists every `Default…` type. The documentation and results pass (findings `N104`–`N111`) re-measured
the 2026-08-19 battery — `<para>` 0 · `<list>` 0 · severity glyphs 0 · `<remarks>` over the cap 0 of 275 — and found clean:
`IsSuccess` + null-check consumption (0; every `IsFailure` call uses the `out` pair) · `required` without a rule (0) · unsealed
role types (only the 2 generic repository bases, open by design) · `services.Configure<T>` on our own types (0; all 6 hits are
framework option types).

---

## Naming and shape

Convention prerequisites: [full-tree audit and additive sweep tasks](../../../system/sessions/backend-beta-build/conventions-audit.md)
(2026-09-09). Complete that convention pass before resuming these SDK rows. Existing IDs remain unchanged;
new source-level consequences receive SDK rows when their convention is settled. The SDK is developer-owned,
with no production consumers; breaking changes are permitted. The release follows SDK verification.

| # | Change | Where | Source |
|---|---|---|---|
| ✅ N2 | ~~Introduce `Outcome`~~ — superseded by `N15`; the application shape is `{Noun}Model` | SDK + products | `core/mla/constructs/data/model.md` |
| ✅ N6 | Gate 27 bare-noun statics against `constructs.md:228`, rename or instance-ify per verdict. The battery over-reports: `sweep.sh` greps for statics not ending `Constants|Extensions|Mapper` and knows neither the `Factory` carve-out at `:210` nor the two gates. **Landed:** `GeohashEncoder` → `GeohashMapper` (both gates pass, `:244` still demands a form; its namespace also said `Geo.GeohashEncoder`, named after the type rather than the folder) · `WebhookSignatureHasher` → instance behind `IWebhookSignatureHasher`, HMAC-SHA256 being a real algorithm that fails Simple. **No change:** `AppErrorFactory` · `AppResultFactory` · `ClaimProviderProfileFactory` — both gates pass and `:206` names `AppErrorFactory` as the worked example. `DbUpProviderFactory` → `DbUpExtensions` in `DbUp/Extensions/`, as `UsePostgres()` / `UseSqlServer()` / `UseMySql()` on `DbUpOptions`: it had zero call sites and existed only for a host to assign `UpgradeEngineFactory`, so it is configuration surface rather than a factory the SDK calls, and `Extensions` is one of the three allowed static forms. Named for the domain, not the type it extends (`extensions.md:49`), and filed under `Extensions/` (`:12`). `CronExpressionParser` → `ICronExpressionParser` and `GeoJsonSerializer` → `IGeoJsonSerializer`, both instance: parsing and JSON reading are real algorithms, so both fail Simple. Consumers re-pin — `dev-cycle.md` § *A consumer never gates an SDK fix*. `TestJson` → `TestJsonConstants` — a `static readonly JsonSerializerOptions` is a value, and `:218` names `JsonOptionsConstants` as the precedent. `BogusFakerFactory` passes both gates. `SagaTestHarness` earned a new carve-out instead of a rename: a non-generic companion to `SagaTestHarness<TState>` takes the generic type's exact name, the way `Result` sits beside `Result<T>`. **`sweep.sh` now counts `internal` statics too: 27, not 12.** 13 renamed in one batch — 4 value tables to `*Constants` (`AzureServiceBusHeaderConstants` · `MessagingDiagnosticConstants` · `KeySizeConstants` · `RedisStreamsFieldConstants`) and 9 deterministic transforms to `*Mapper` (`KafkaTopicNameMapper` · `NatsWireFormatMapper` · `OutboxDispatchHeaderMapper` · `CaptionTimecodeMapper` · …). `AesGcmCipher` and `HashChainHasher` went instance, both real algorithms failing Simple, each held as a field by the seam that already owned the registration choice (`ICryptoCore`, the sealer and verifier). `NamingConventionsMarker` was dead and is gone, its doc kept as the file's namespace comment. The last 6: `OAuthBaseline` → `OAuthExtensions` in `OAuth/Extensions/`, its two members now extending `OAuthOptions` across 17 provider registrations · `NatsTopology` and `RedisStreamsTopology` → `*TopologyBroker` instances, both doing async broker I/O · `CliCommands` → `CliCommandBuilder`, `Builder` being the keep-list role for assembling a tree · `AdapterOwnedHeaderContract` → instance, its publish loop being I/O · `WebhookAddressPolicy` split by role into `WebhookAddressMapper` (3 pure predicates, static) and `WebhookSsrfGuard` (the DNS + socket connect callback, instance) — one type had been holding both. **Closed: the battery reports only the 5 compliant `*Factory`, one of which (`AppErrorProblemDetailsFactory`) belongs to `car-t-012`.** `AppErrorProblemDetailsFactory` stays with `car-t-012` | SDK | `core/mla/constructs/constructs.md` § *Folds* |
| ✅ N7 | resolved — it ships as `SqlNamingMapper`, and the battery's bare-noun static check no longer lists it | SDK | `core/mla/constructs/constructs.md:145` |
| ✅ N8 | `StyleSpecNormalizer` → `StyleSpecMapper` | SDK `src/Codes/Models/Style/` | decided, unshipped |
| ✅ N10 | Closed as moot — `ValidationResult` no longer exists, and the `ValidationError?` return stays by `N81`'s carve-out: `Validate` is the return half of a declared bridge whose throw half is `ValidateAndThrow`. `R4` was never written to this file, so nothing else hangs on it | the rename is moot — `ValidationResult` no longer exists; `IValidator.Validate` returns `ValidationError?`, so the open half is wrapping it in `Result<T>`, which is `R4` | SDK `Foundation/Validation/` | `results.md` § *What returns a `Result`* |
| ✗ N11 | refuted by `N69` — a `Mapper` with no failure mode returns bare; the role never settles it | SDK + products | `results.md` § *What returns a `Result`* |
| ✅ N12 | `IErrorMessageResolver` → `…Mapper`; `Resolver` is a folded suffix | SDK `Foundation/Errors/` | `core/mla/constructs/constructs.md` § *Folds* |
| ✅ N13 | `IFieldErrorMessageResolver` → `…Mapper` | SDK `Foundation/Validation/` | same |
| ✅ N21 | Add `IKeylessEntity : IEntity` and `ICompositeKeyEntity : IEntity` beside `IKeyedEntity<TId>` | SDK `Data/Abstractions/` | `core/mla/domains/persistence/entities/entity-contracts.md` § *Identity* |
| ✅ N22 | 3 concrete types implement bare `IEntity` — `IdentityUserRole` · `IdentityUserLogin` · `IdentityUserToken`, all composite join rows | SDK `Identity/Core/IdentityRelations.cs:7,55,73` | same |
| ✅ N23 | Bare `IEntity` never on a concrete type — audit every implementer once N21 lands | SDK + products | same |
| ✅ N24 | **Shipped 2026-09-03** — `StoredJsonConstants.Default` · `StoredJsonOptionsFactory.Create(params modifiers)` · `SubtypeRegistry<TBase, TKind>` (completeness in the ctor, `[JsonDerivedType]` checked against the enum when both exist) · `SubtypeRegistryExtensions.ToJsonModifier`; `JsonValueConverter<T>` / `JsonValueComparer<T>` default to the stored preset and `HasJsonConversion<T>(options?)` takes a pinned instance. 7 tests in `Foundation.Tests/Serialization/`; `serialization.md` § *Stored JSON* documents both paths. Product follow-up (smart-qr `J1` / `J2`): after the publish, `JsonbOptions` · `SubtypeRegistry` · `SubtypeRegistryJsonExtensions` in `SmartQr.Common.Domain.Serialization` are the SDK's, `CodeContentJson` / `CodeRuleJson` collapse into a `CodeRuleJsonConstants` holder or go, and `CodeEntityConfiguration` calls `HasJsonConversion(options)`. Design: **Settled 2026-09-03 — both declaration styles ship.** Shared: `StoredJsonConstants.Default` (Web defaults · camelCase string enums · `AllowOutOfOrderMetadataProperties` for jsonb key order · NodaTime), `JsonValueConverter<T>` / `JsonValueComparer<T>` default to it, `HasJsonConversion<T>(JsonSerializerOptions? options = null)`. **B** — unions declared on the base with `[JsonPolymorphic]` + `[JsonDerivedType]`, nothing registered, the SDK's own `GradientSpec` shape. **A** — `SubtypeRegistry<TBase, TKind>` lifted from smart-qr with `ToJsonModifier()`, and `StoredJsonOptionsFactory.Create(params Action<JsonTypeInfo>[] modifiers)` building the pinned instance a product holds in a `{Root}JsonConstants` holder and hands to `HasJsonConversion`; `SubtypeRegistry.Verify()` checks a base's attributes against the enum when both are present. The per-type `Json` seam class goes either way → `N25` | Ship a JSON serializer holding options in a type-keyed dictionary; products stop declaring a `static readonly JsonSerializerOptions` · `JsonValueConverter` / `JsonValueComparer` default to a bare Web options instance and `HasJsonConversion<T>()` offers no override, so an enum stored through it is an integer while the wire carries the camelCase string — whichever shape lands, `HasJsonConversion` defaults to the stored preset | SDK, lifted from `SmartQr.Common.Domain.Serialization.Json` | `smart-qr/be-sweep-handoff.md` § *JSON seams* |
| ✅ N25 | Confirmed/applied 2026-09-13: retire the per-type `Json` role and static exception. Use `StoredJsonConstants.Default`, SDK converters and direct `JsonSerializer` calls; pin custom document options through `StoredJsonOptionsFactory` only when the stored format requires them. Persistence owns the separate stored contract, including preservation of absence/error behavior during migration. Current SDK source has no per-type `*Json` wrapper declaration to remove; codecs/converters remain distinct. N24 product migration follow-up remains. Convention closure only; no new runtime verification or release claimed. | conventions | `core/mla/domains/persistence/stored-json.md` |
| 🔄 N26 | 46 bare-noun `static class` types are none of the three forms — `SqlNaming` · `Geohash` · `Polling` · `QuietZone` · `CaseConverter` · … | SDK, whole tree | `core/mla/constructs/constructs.md:145` |
| ✅ N27 | `ColumnCase` / `ParameterCase` left the static as `SqlNamingOptions`, and every mapper method now takes its `CaseStyle` as an argument — `Options`, not `Settings`, because the caller supplies it in code | SDK `Data/Dapper/` | same + § *`Settings` vs `Options`* |
| ✅ N28 | Repoint every `SqlNaming.*` call site and the 14 `dapper.md` citations after N26 | SDK + products + conventions | `core/mla/domains/persistence/access/dapper/dapper.md:104-226` |
| ✅ N29 | `HostedService` folds into `BackgroundService` — rename `EfMigrationsHostedService<T>` and `DbUpHostedService` | SDK `Data/Migrations/` | `core/mla/constructs/constructs.md` § *Folds* |
| ✅ N34 | Closed 2026-09-03 — the battery reports 0 fold and 0 banned hits. The 4 `Observer` types had already become `…ObservingInterceptor` under `C8`. The five disputes each went where the convention text points: `IMasterKeyProvider` → `IMasterKeyBroker` + `EnvironmentMasterKeyBroker`, by `N92`'s precedent — the seam exists so a KMS/HSM swap stops there, and `repository.md`'s "rows in, rows out, and nothing else" never fit one configured secret · `ClaimProviderProfile` → `ClaimProviderSpec` (+ `…SpecFactory`, `Specs`), because `options.md` forbids a positional init-only record and the type declares a mapping's inputs rather than mapping; the keep-list `Spec` row widened past renderers and the `Profile` fold row split to say so · `UserAccountManager` → `UserAccountService`, on a primary ctor — it normalizes, stamps and enforces uniqueness over `IUserRepository`, which is orchestration, not "data in, data out"; the counter (that knowledge belongs behind the repository) is a restructure, parked · `IAuditCurrentUserAccessor` → `IAuditCurrentUserService`, rename only — the counter's unification with `ICurrentUser` is `N94` · `DatabaseProvider` stays: an enum names a value set, not a role, so `sweep.sh` now excludes the 39 enums from checks 1-2; `DatabaseEngine` remains a taste call, not a convention one. 0 consumer files reference a renamed type | 19 of 29 folded and banned names landed — `Provider` → `Service` on the access-token, reply-address and topology seams · `Resolver` → `Mapper` on message types and `Service` on the tenant id · `Keeper` → `SealService` · `Scheduler` → `IDelayedDeliveryService` and `ISagaTimeoutService`, the `Service` arm `N93` added · `Strategy` → `IOutboxClaimRepository`. Every `Default…` prefix dropped, since `constructs.md:132` leaves a lone implementation the role's own name. **Left: 10** — the 4 `Observer` types wait on the contract-naming fork, and `IMasterKeyProvider` (Repository or Service), `ClaimProviderProfile`, `UserAccountManager`, `IAuditCurrentUserAccessor` and the `DatabaseProvider` enum each carry a live dispute | SDK | 28 fold hits + 5 banned names across 973 types — `Provider` 9 · `Resolver` 5 (3 `Source` hits cleared by `N92`) · `Observer` 4 · `Scheduler` 4 · `Source` 3 · `Keeper` 2 · `Profile` 1; banned `Strategy` 3 · `Manager` 1 · `Accessor` 1. Recounted 2026-08-22, down from 56 fold hits + 6 banned | SDK | `core/mla/constructs/constructs.md` § *Folds* · § *Banned* |
| N94 | `ICurrentUser` carries no role suffix and holds the same role as `IAuditCurrentUserService` under a second name — unify on one `ICurrentUserService` once the audit and soft-delete interceptors resolve the user per save instead of per construction: `AddEfInterceptor` registers singletons, while `CookieCurrentUser` is scoped with a per-instance cache, so today the two contracts exist because of the lifetimes, not the naming. 4 smart-qr controllers inject `ICurrentUser`; a consumer never gates the fix | SDK `Data/EntityFrameworkCore/Audit/` + `Identity/CurrentUser/` | `core/mla/constructs/constructs.md` § *Role and shape* · § *Banned* |
| ✅ N95 | 7 lone implementations lose the `Default…` prefix — `ErrorNatureClassifier` · `EventFaultClassifier` · `RetryPolicy` · `EndpointNameMapper` · `ErrorHttpStatusCodeMapper` · `ErrorMessageMapper` · `FieldErrorMessageMapper` (its second implementer is a test double, which is not a sibling). `DefaultMessagingMetrics` and `DefaultEventResiliencePipeline` keep theirs — each has a shipped sibling. `N34` had counted only the 4 it renamed; the battery had no check for the prefix, so `sweep.sh` gained check 9. 1 consumer file constructs `DefaultErrorHttpStatusCodeMapper` (`SecretsVault.Tests/Results/ApiResultsTests.cs`) — product row | SDK | `core/mla/constructs/constructs.md:132` § *Role and shape* |
| ✅ N96 | 3 files named `struct.cs` renamed for their types — `Mediator/Unit.cs` · `Identity/Core/IdentityError.cs` · `Identity/Claims/AvatarSynthesisContext.cs`; `UserAccountServiceTests.cs` had kept its old class name after `N34`, fixed | SDK | `core/mla/mla.md` § *One type, one file* |
| ✅ N97 | `IEmailSender` → `IEmailBroker`, with `MailKitEmailBroker` · `SendGridEmailBroker` · `SesEmailBroker` and the three `Add*EmailSender` registrations → `Add*EmailBroker`. Three providers behind one contract that speaks our `EmailMessage` is `broker.md`'s own definition, and the interface keeps the provider's name out. Summaries open with *Integrates*. 0 consumer files. `Acs` · `FluentEmail` · `Mailgun` · `Postmark` are empty local folders, not providers | SDK `Comms/Email/` | `core/mla/constructs/behavior/broker.md` |
| ✅ N98 | `MigrationChecksumExtensions` → `IMigrationChecksumHasher` / `MigrationChecksumHasher` — SHA-256 fails the Simple gate by the doc's own worked example, the reasoning that moved `HashChainHasher` and `WebhookSignatureHasher` under `N6`. The runner takes it through its primary ctor and `AddDatabaseBespokeMigrations` registers it; 1 call site | SDK `Data/Migrations/Bespoke/` | `core/mla/constructs/constructs.md` § *Static or instance* |
| ✗ N99 | refuted — `options.md:13` bans an `Options/` folder for the *records*, so their `Add*` stays visible beside them; `Foundation/Options/` holds the registration recipe itself (`OptionsRegistrationExtensions`), whose domain is options registration, filed the way every `*ServiceCollectionExtensions` sits beside its domain. `extensions.md:12`'s `Extensions/` folder governs the non-registration statics (`DbUp/Extensions/` · `OAuth/Extensions/`, `N6`) | SDK | `core/mla/constructs/data/options.md:13` |
| 🔄 N100 | **Per case, not a fold and not a blanket kinds table** (2026-09-03). `Log` — three cases, not one: `SagaLog` was a `[LoggerMessage]` holder → `SagaLogExtensions` with `this ILogger` receivers, the `Extensions` static form ✅ · `IWebhookDeliveryLog` was a terminal-outcome sink that persists nothing → `IWebhookDeliveryLoggingService` + `NoopWebhookDeliveryLoggingService`, a `Service` named for its work ✅ · `InterceptorLog` · `RecordedMessageLog` · `RecordedTransitionLog` are test recorders in the shipped `Testing.*` packages — open. `Capabilities` (`ITransportCapabilities` + 6 per-transport flag sets the dispatch pipeline reads to pick native vs emulated) stays as named and enters the keep-list as a kind of `Model`. Still to discuss, one at a time: `Classifier` · `Parser` (4 + 14) · `Coordinator` · `Runner` (6) · `Exporter` · `Formatter` · `Humanizer` (7) — no merge into `Renderer` | Five suffixes the keep-list definitions already fold — refuted as folds, each case its own discussion | SDK + conventions | `core/mla/constructs/constructs.md` § *Keep-list* |
| N101 | Suffixes in use that no keep-list row recognises, a coining call — `Transport` (16: `IReceiveTransport` · `ISendTransport` and 5 provider pairs; neither `Client`, which speaks the provider's shape, nor `Broker`, which speaks ours, fits, since the transport *is* the wire) · `Parser` (14: `ICronExpressionParser`, the caption parsers, CSV; a total text → structure transform that owns a failure mode, which `mapper.md` denies a `Mapper`) · `Serializer` (6: `IMessageSerializer` and its 3 codecs, `IGeoJsonSerializer`; a pluggable multi-format codec, not the one-type `Json` seam) · `Verifier` (4: `GoogleIdTokenVerifier` · `HashChainVerifier`; joins the `Cipher · Hasher · Issuer · Authenticator` row) · `Bus` · `Session` · `Metrics` · `Envelope` (2 each). § *Adding a new suffix* reserves a coining for the developer; the four large ones are used consistently and read as roles, the four small ones as deliberate one-offs | SDK + conventions | `core/mla/constructs/constructs.md` § *Adding a new suffix* |
| N102 | `ICryptoCore` / `CryptoCore` — `Core` names nothing, in the vein of banned `Engine`, and the type is one encrypt/decrypt pair under a caller-supplied key, which the keep-list's `Cipher` row covers: `IValueCipher` / `ValueCipher`, with `AesGcmCipher` staying the shape it wraps. 4 secrets-vault files reference it — product row | SDK `Foundation/Security/` | `core/mla/constructs/constructs.md:71` |
| N103 | 5 of 6 `Registry` types in `Messaging/` answer a miss with `TryGetX(out …)` where `registry.md:59` demands a throw ("a silent miss hides a wiring fault") — `MessageTypeRegistry` (`:43,48`) · `DestinationBindingRegistry` (`:30`) · `EventDispatcherRegistry` (`:15`) · `PendingRequestRegistry` (`:45`) · `MessageSerializerRegistry` (`:62`). An unregistered wire type is exactly the wiring fault the rule targets, so `MessageTypeRegistry` goes first; a dispatcher or pending-request miss may be a legitimate 0..N or a race, so each of the other four decides whether its miss is a fault before it changes. `ConsumedMessageTypeRegistry` has no lookup at all — `Add` + `Types` — so it may be a `Tracker` | SDK `Messaging/` | `core/mla/constructs/behavior/registry.md:59` |
| N104 | Two `Result` gaps the role docs close — `RequestClient.GetResponseAsync` (`:42,50`) throws `RequestTimeoutException` / `RequestFaultException` for a per-call timeout or fault, an expected failure on a `Client`, so it returns `Result<TResponse>`; `FileSystemMigrationBroker:25` and `EmbeddedResourceMigrationBroker:51,57` throw `InvalidOperationException` for a migration missing its rollback script, and `Scan()` at `MigrationRunnerService:248` lets that escape every `Result`-returning method, so `IMigrationBroker.Read` returns `Result` and `Scan()` needs no change once it does. Each lands one type at a time with the suite green — `N81`'s protocol | SDK `Messaging/Transport/` + `Data/Migrations/Bespoke/` | `shapes/service/platform/responses/results.md` § *What returns a `Result`* |
| ✗ N105 | `EnumNameMapper.Parse` / `TryParse` — refuted by `R2`: `TryX` + `bool` is a shape choice, and `Parse` is the declared throw half of that pair (the `int.Parse` / `int.TryParse` bridge), the same carve-out `N10` gave `Validate` / `ValidateAndThrow` | SDK `Foundation/Naming/` | `R2` · `N10` |
| ✅ N106 | The `configure` branch bug — 5 registrations (`AddHashChain` ×2 overloads · `AddOtpService` · `AddGuestSession` · `AddCurrentUser`) registered the bare record only in the `else` branch, so a caller who configured them got a DI failure on the first resolve of `HashChainSealer` / `NumericOtpCodeGenerator` / `CookieGuestSession` / `CookieCurrentUser`; `AddEnvelopeCryptography` had the same shape with the braces dropped, so its projection ran by accident. All 5 are on `N47`'s construct → invoke → register shape, and `ClaimCheckOptions`' 6 rules now run at boot through `AddValidatedOptions` instead of at first resolve. 4 regression tests cover the configured path — `Identity.Tests/ConfiguredRegistrationTests.cs` · `Foundation.Tests/Security/EnvelopeCryptographyRegistrationTests.cs` | SDK | `core/mla/components/options.md` § *Registration* |
| ✅ N107 | Doc markup — 38 `<b>` / `<i>` tags stripped across 29 files, and the banned `Defines the contract for` opener rewritten on 15 contracts (the agent had counted 6) to `Defines {capability}` per `summary.md` § *Summary* | SDK | `core/lla/notation/documentation/summary.md` |
| N108 | Summary starters — 270 of 308 checkable types open with something other than their role's starter: `Repository` → *Accesses* 35/35 · `Options` → *Holds* 72/75 · `enum` → *Refers to* 40/40 · `Mapper` → *Maps* 25/35 · `Service` → *Provides* 19/24 · `Constants` → *Holds* 20/20 · `Handler` → *Handles* 16/30 · `Client` → *Connects* 8/8 · `Registry` → *Binds* 7/7 · `Broker` → *Integrates* 7/9 · `Result` → *Represents* 7/12 · `BackgroundService` → *Runs* / *Schedules* 4/4 · `Validator` 4/4 · `Factory` → *Creates* 3/8 · `Entity` → *Represents* 2/6 · `Settings` 1/1. The rule postdates every summary. `summary.md` gives a behavior *interface* `Defines {capability}`, so the counts over-count contracts — the class-only number is lower. Two conventions-side conflicts cleared first: `components/enums.md` said *Defines* and `components/settings.md` said *Configuration for*; both now match their construct docs. A pass per role, opening clause only, keeping every fact; stop when the fix rate inverts (the handoff's doc-agent lesson) | SDK + conventions | `core/lla/notation/documentation/summary.md` · each role doc § *Type doc* |
| ✅ N109 | Construct form — `DbUpBackgroundService` and `EfMigrationsBackgroundService` moved onto primary ctors; `SealService.GenerateDataKey` and `TotpService.GenerateSecret` took block bodies | SDK | `core/mla/constructs/behavior/service.md:36` · `style.md` § *The body* |
| 🔄 N110 | Inline `//` runs over the one-line cap — 40 non-test files. The 3 worst landed: `EfOutbox.cs:42` to one line, `WebhookPublisher.cs:27-36` (a 10-line rationale) to one line plus `webhooks.md` § *Payload contract*, `TransportConsumerBackgroundService.StopAsync` to a 3-line `<remarks>`. 37 files left, one pass; a rationale moves to the folder doc, an ordering guarantee to `<remarks>`, a defense of a rejected shape goes | SDK | `core/lla/notation/documentation/inline.md` § *What it carries* · § *Exclusions* |
| N111 | `AddOptions<T>()` outside the recipe — 38 sites in 26 files, against `N47`'s "7 kept". Each is one of three: a `PostConfigure` composer (kept by design), a rule-carrying record (→ `AddValidatedOptions`, as `ClaimCheckOptions` did under `N106`), or a rule-less record that takes `N47`'s `new T()` + `TryAddSingleton`. One sorting pass; none validates at boot today | SDK | `core/mla/components/options.md` § *Registration* |
| ✅ N112 | Resolved through the `N6` gate — `SagaLog` → `SagaLogExtensions` (`this ILogger` receivers) · `MigrationConventions` → `MigrationConstants` (file names, directives, the folder-name regex as a compiled value) · `MessageObserverNotifications` → `ObservingInterceptorExtensions` (fan-out extensions over the interceptor arrays) · `CaptionFormatDetector` → `CaptionFormatMapper` (leading content → `CaptionFormat`, both gates pass, `TryDetect` kept as the `TryX` shape) | 4 bare-noun statics the battery had missed — check 3 grepped `static class` and skipped `static partial class`, now widened: `SagaLog` (a `[LoggerMessage]` holder, static because its coordinator is generic) · `CaptionFormatDetector` · `MessageObserverNotifications` · `MigrationConventions`. Each takes the `N6` gate — `Constants` / `Extensions` / `Mapper`, or an instance; `SagaLog`'s answer sets the idiom for every log-message holder | SDK | `core/mla/constructs/constructs.md:145` · § *Static or instance* |
| ✅ N113 | 47 of 47 converted to body properties — `required` where the ctor demanded the value, the original default otherwise — and 136 construction sites across 38 files became initializers; 3 `<paramref>` cross-refs became `<see cref>`. One carve-out: `Messaging.Tests/SerializerPayload.Optional` stays non-required, because the wire preset omits a null on write and a `required` member then fails the read — the shape any `required` nullable member takes under `WhenWritingNull`. Check 10 prints nothing; 342/342 | Positional records are banned for a data carrier (`lla/constructs.md:68,90,169` — body properties, never a primary ctor) and 47 ship positional: 31 in the SDK (`WebhookDeliveryRecord` · `EmailMessage` · `EncryptedPayload` · `OtpRecord` · `DeadLetterRecord` · `OutboxRecord` · `RetryConfig` · `EventSagaResult` · the GeoJSON family · `ClaimProviderSpec` · `IdentityError` · `AvatarSynthesisContext` · …) and 16 test events in `Messaging.Tests`. Each becomes `{ get; init; }` body properties with `required` where the ctor demanded the value, `<param>` docs move onto the members, every `new X(a, b)` call site becomes an initializer, `with` keeps working. `sweep.sh` check 10 lists them | SDK | `core/lla/constructs/constructs.md:68` § *Data components* |
| ✅ N38 | Section banners in `//` are gone repo-wide — the rule bans the construct, not one glyph, so the recount widened from 9 `──` banners in 4 source files to 25 across 8, the 16 extra being `// ---` in the test projects. Member groups past 60 lines took `#region`; the rest were deleted, since a banner inside a method body groups statements and the rule scopes a region to members | SDK | `core/lla/constructs/constructs.md` |
| ✅ N39 | **[+]** the lookup normalizer collapsed into `NamingExtensions.ToCanonical()` — the interface, its impl and its DI registration are gone, and `CaseStringExtensions` took the domain name the rule asks for | SDK `Foundation/Naming/` | `core/mla/constructs/behavior/extensions.md` § *Type name* |
| ✅ N40 | **[+]** `Options` gained a construct doc and a component doc, indexed from `data.md` · `components.md` · the keep-list | conventions | `core/mla/constructs/data/options.md` |
| ✅ N41 | **[+]** `patterns.md:119,148` now links the owner instead of restating the rule | conventions | `conventions.md` § *One owner per rule* |
| ✅ N42 | **[+]** 59 `*Options` classes became `sealed record` so `with` works — `TestAuthOptions` stays a class, it derives from a framework type | SDK | `core/mla/constructs/data/options.md` § *Declaration* |
| ✅ N43 | **[+]** 41 `{ get; init; }` members on `*Options` types became `{ get; set; }` for the delegate | SDK | same |
| ✅ N45 | **[+]** `{Capability}Extensions` and `{Primitive}Extensions` added to the naming rule; `CaseStringExtensions` → `CasingExtensions` | conventions + SDK | `core/mla/constructs/behavior/extensions.md` § *Type name* |
| ✅ N46 | **[+]** `DbUpOptions.ConnectionString` is `required` and arrives as an `AddDbUpRunner` parameter | SDK `Data/Migrations/DbUp/` | `core/mla/components/options.md` § *Registration* |
| ✅ N57 | Settled as written — the placeholder stays, because a `required` member cannot survive the `PostConfigure` chain, and `N47` kept this type on `AddOptions<T>()` for exactly that chain | **[+]** `AzureServiceBusOptions.ConnectionString` keeps its placeholder — the type stays on `AddOptions<T>()` for its `PostConfigure` chain, which cannot construct a `required` member | SDK `Messaging/AzureServiceBus/` | same |
| ✅ N58 | **[+]** the defaults rule lifted to `constructs.md` § *`Settings` vs `Options`* and both docs link it; location split by shape — a service uses the layer's `Settings/`, a library sits beside its `Add*` | conventions | `core/mla/constructs/constructs.md:112` |
| ✅ N47 | **[+]** 19 of 27 `AddOptions<T>()` registrations bought nothing and moved to `new T()` + `TryAddSingleton`; consumers dropped `IOptions<T>` and `.Value`. The 7 kept are the 5 transport→topology `PostConfigure` composers, `TopologyOptions`, and `WebhookOptions` for its `.Validate` delegates | SDK | `core/mla/components/options.md` § *Registration* |
| ✅ N48 | The last two `ValidateOnStart()` calls moved onto the seam and `ValidateOnStart` now appears once in the whole SDK, inside it. `DatabaseSettings` had validated nothing; it asserts its connection string | **[+]** 4 of 5 `ValidateOnStart()` calls carry no `.Validate` delegate and no `ValidateDataAnnotations`, so they validate nothing — `DbUp` · `EfMigrations` · `Database` ×2 | SDK | `core/mla/components/settings.md` § *Registration* |
| ✅ N49 | One seam covers both — a bound section and a delegate-filled record differ only in how they are filled, so `AddValidatedSettings` and `AddValidatedOptions` share the same sealing step | **[+]** one validation seam has to cover `Settings` and `Options` alike — a bound section and a delegate-filled instance validate through the same rules, so neither doc should ossify around `ValidateOnStart` | SDK + conventions | queued design |
| ✅ N50 | **[+]** No conversion needed: every `IOptionsMonitor<T>` injection in the SDK is one of the 4 named-options sites, which is the one shape the monitor exists for. The row's premise was unsound as stated — `reloadOnChange` lives in the host's configuration builder, not in the SDK, so counting zero here proves nothing about whether a value can reload | SDK | `core/mla/components/options.md` § *Registration* |
| ✅ N44 | **[+]** 7 broken relative links in the backend conventions repointed — 4 in `time.md`, 2 pre-move frontend paths, 1 repo path | conventions | mechanical |
| ✅ N86 | 12 types renamed onto the single `Repository` role, engine in the prefix — `EfUserRepository` · `DapperRepository` · `LocalFileBlobRepository` · `HybridCacheRepository` · `InMemoryTenantRepository` · `MemoryOtpRepository`, and `ICacheRepository` · `IBlobRepository` · `IOtpRepository` · `IIdempotencyRepository` on the contract side. `Storage` and `Cache` stop being roles, so swapping Postgres for Redis is a host-configuration line and no use case learns it happened | SDK | `core/mla/constructs/constructs.md:101` § *`Repository`* |
| ✅ N85 | folded into `N86` — ownership, contract shape and composition were each tried as the discriminator and each read an implementation fact, so none can carry a role | SDK | same |
| ✅ N74 | `AddValidatedOptions` and `AddValidatedSettings` are the one recipe — fill or bind, apply the caller's rules, add the data annotations, check at boot, project the record. `validate` is a required parameter, so a registration cannot state zero rules and still call `ValidateOnStart`, which is what `N48` measured | One registration mechanism for `Options` and `Settings`, validation first — `required` is decorative under `AddOptions<T>` because `Activator` bypasses it, so validation is the only enforcement | SDK | raised as `car-t-013`; rides with `N47` · `N69` |
| ✅ N87 | `MigrationScannerService` folded into `MigrationRunnerService` as a private `Scan()`, and `IMigrationScanner` deleted — source pluggability already lives in `IMigrationSource` (2 implementations plus a `sourceFactory` registration hook), while the scanner seam had 1 implementation, 1 consumer and no test fake, and no test called `Scan()` directly. DI registration dropped; Migrations suite 14/14 | SDK `Data/Migrations/Bespoke/` | `core/mla/constructs/constructs.md` § *Role and shape* |
| ✅ N88 | `CliRunner` → `CliRunnerService`, an instance `sealed partial class` taking `IMigrationsPathBroker`; `CliCommands.Build()` constructs one and threads it through the 6 command builders. `partial` stays — it carries the `[GeneratedRegex]` `OrdinalPrefix()`, which is a source-generator requirement, not an extensions marker. `sweep.sh` still greps `public static class` and would miss the next `internal` one — widening it stays open. Originally: `CliRunner` was an `internal static partial class` with 8 public static methods and a `BuildProvider` that constructs a `ServiceProvider` — a bare-noun static is allowed only for `Constants` / `Extensions` / `Mapper`, so this becomes an instance service. `sweep.sh` greps `public static class` and missed it because the type is `internal` — widen the check | SDK `Data/Migrations/cli/CliRunner.cs:18` | `core/mla/constructs/constructs.md:145` |
| ✅ N89 | `MigrationsPathResolver` → `MigrationsPathBroker` behind `IMigrationsPathBroker`, returning `Result<string>`. **`Broker`, not `Service`:** `constructs.md:188` folds `Resolver` by what it touches — pure → `Mapper`, out-of-process → `Broker`, injected collaborators → `Service` — and a directory probe is filesystem I/O. Originally: `MigrationsPathResolver` was an `internal static class` carrying a folded suffix — `Resolver` folds to `Mapper` (`N12` / `N13`), and the path lookup is meant to be overridable, which a static cannot be. Becomes an instance service behind a contract | SDK `Data/Migrations/cli/MigrationsPathResolver.cs:4` | `core/mla/constructs/constructs.md` § *Folds* + `:145` |
| ✅ N90 | `IErrorMessageMapper` and `IFieldErrorMessageMapper` moved to `Web/ErrorMapping/`, and the DI half of `ExceptionMapping.cs` split into its own `*ServiceCollectionExtensions.cs` — `Foundation/Errors/` core is BCL-only, so the migrator CLI's exclude narrowed from 2 named files to the `*ServiceCollectionExtensions.cs` pattern. Originally: `IErrorMessageMapper` imports `Microsoft.AspNetCore.Http` and `ExceptionMapping` imports the DI container, both from `Foundation/Errors/` — a Foundation primitive must not know about web or hosting. Surfaced when the web-free migrator CLI had to exclude exactly these 2 files by name to link the `Result` carrier; move them to the web layer and the exclude list goes away | SDK `Foundation/Errors/` + `Data/Migrations/cli/*.csproj` | `core/mla/constructs/constructs.md` § *Role and shape* |
| ✅ N92 | `IMigrationSource` → `IMigrationBroker`, with `FileSystemMigrationBroker` and `EmbeddedResourceMigrationBroker` — reading migrations off disk or out of an assembly is the app-side seam over an external store, which `broker.md` names outright ("store a file"), and the seam exists so a provider swap stops there. The folds table sent every `Source` to `Generator`; that row now splits — derives a value → `Generator`, reads one from an external store → `Broker`. Clears 3 of `N34`'s fold hits | SDK `Data/Migrations/Bespoke/` + conventions | `core/mla/constructs/constructs.md:190` |
| ✅ N93 | Landed on both sides — the folds row reads `BackgroundService` · `Service` with the timer / request split, and the code sits on it as `IDelayedDeliveryService` and `ISagaTimeoutService` (`N34`) | The folds table sent every `Scheduler` to `BackgroundService` on the grounds that a poller schedules nothing. That fits a poller; `IEventScheduler` and `ISagaTimeoutScheduler` are called BY application code to deliver something later, so neither is host-run. Row split: runs itself on a timer → `BackgroundService`, takes a request to deliver later → `Service`. Same shape as the `Resolver` and `Source` splits | conventions | `core/mla/constructs/constructs.md` § *Folds* |
| ✗ N94 | Coin `Observer` as a keep-list role — **refuted**, 3 of 3 refuters voted to kill. The word names a position and a permission, never a verb, so § *Adding a new suffix* returns `Service` at its own gate. The read-only claim the row would rest on is also false as shipped (`C8`). The fold row is recut rather than dropped: its `Handler` and `BackgroundService` arms were both wrong for these three types, and a third arm now carries them | conventions | `core/mla/constructs/constructs.md` § *Folds* |
| ✅ N95 | `strategies.md` contradicted itself — § *Shape* required the contract be `suffixed Strategy` while § *Naming* forbade the suffix and § *Banned* bans the word outright. § *Shape* was the stale clause, since the keep-list owns the vocabulary and a pattern doc owns only how the pattern behaves (`constructs.md:15`). Rewritten to name the role the decision serves | conventions | `core/mla/constructs/patterns/strategies.md` § *Shape* |
| ✅ N96 | `AppErrorObserver` → `ErrorRecordingService`, named for the work its own summary already stated. Nothing observes it — `ExceptionMappingInterceptor` calls `Record` directly — so it was never a chain step | `AppErrorObserver` is the last `Observer` in the tree and it is not a chain step at all — no pipeline calls it, `ExceptionMappingInterceptor` calls `Record` directly, and its own summary starts with **Records**. Originally: `AppErrorObserver` carries the `Observer` suffix on a type sitting on no pipeline, notified by nobody, and called imperatively as `observer.Record(error, exception)` from `ExceptionToResultBehavior`. Its own summary starts with **Records**, which is the verb the name should carry | SDK `Observability/Errors/` | `core/mla/constructs/constructs.md` § *Folds* |
| ✅ N97 | Every file holds one type — 106 multi-type files split into 434, each named for what it holds. A generic keeps its non-generic companion, which the rule allows (`Result.cs` holds both arities). 7 file names that never matched their type were corrected alongside. The first pass cut a type's doc comment onto the previous type; the compiler caught it as CS1587/CS1591 and 4 identity types that had been sharing one summary now each state what they are | 106 files hold 2 or more top-level types, against `mla.md:21` § *One type, one file*, which is REQUIRED and allows only a generic beside its non-generic companion. Worst: `RequestClient.cs` 13, `MessagingReliability.cs` 12, `Topology.cs` 11, `RedisStreamsTransport.cs` 11, `EventSaga.cs` 11. `MessageSerialization.cs` holds 5 — `IMessageSerializer`, `SystemTextJsonMessageSerializer`, `IMessageTypeMapper`, `MessageTypeRegistry`, `MessageTypeMapper` | SDK | `core/mla/mla.md:21` |
| ✅ N98 | Two words settled for the whole family: a `Handler` is the type the message was addressed to, an `Interceptor` is any step it passes through first, whatever that step does with it. 16 types renamed — the 8 mediator `Behavior`s, the 4 messaging `Filter`s and the 3 observer contracts — with the job in the middle word (`ValidatingInterceptor`, `ClaimCheckRehydratingConsumeInterceptor`, `IConsumeObservingInterceptor`). Keep-list row and `behavior/interceptor.md` written; `Behavior`, `Filter` and `Observer` all fold there. The framework-owned uses stay exempt, the exemption now scoped to a type deriving from a framework base | SDK + conventions | One pipeline family carries three words in code we own — `Filter` (`IConsumeFilter` + 4), `Behavior` (`IPipelineBehavior` + 7) and `Observer` (3 messaging hooks). The framework-owned uses are exempt and stay: `SaveChangesInterceptor` (5), `IExceptionHandler` (3), `AuthorizationHandler`, `AuthenticationHandler`, `DelegatingHandler`, Dapper `TypeHandler` (4), MVC filter, ASP.NET middleware. Settle the vocabulary, then rename ours to it | SDK + conventions | `core/mla/constructs/constructs.md` § *Folds* |

---

## Result pattern

| # | Change | Where | Source |
|---|---|---|---|
| ✗ R1 | refuted by `N69` — the failure mode decides, so "every behavior component" over-reaches | SDK + products | same |
| ✗ R2 | refuted by `N69` — `Extensions` is not exempt either; `TryX` + `bool` stays a shape choice, not an exemption | SDK + products | same |
| ✅ R3 | superseded by `N69` — the failure mode decides, not the role | SDK + products | `results.md` § *What returns a `Result`* |
| ✅ R5 | Wired, not deleted — `AddMediator` registers it first. The audit found 10 paths that legitimately still throw, all of them pre-pipeline (`Mediator.cs:19,52,67,68` run before the pipeline is composed) or the throw-return bridge itself, so the unconditional claim in `results.md:75` was false and is now scoped to a request whose response carries a failure arm and that reaches the pipeline | SDK `Mediator/` | `ideas/exceptions-analysis.md` |
| ✅ R7 | resolved — `results.md` § *Carriers* gives each a lane: `Result<T>` everywhere, `AppResult<TSuccess>` for mediator handlers ↔ controllers, and an inner `Result<T>` maps up in the handler | SDK | `results.md:13` § *Carriers* |
| ✅ R8 | `Result<TSuccess, TFailure>` ships in `Result.cs` beside its two siblings — the same closed DU with the failure arm typed: `Ok` · `Fail` · `IsSuccess` · `Match` · `Map`, plus `ToResult(Func<TFailure, AppError>)` as the documented map-up into the default carrier. `TFailure : notnull`, closed by the caller (an enum or a sealed union), never an `AppError` subclass. 6 tests in `Foundation.Tests/Results/TypedFailureResultTests.cs`; `results.md` § *Carriers* gained the row and § *Typed failure* lost its "does not exist" paragraph. The status-enum idea stays deferred — the carrier never learns a status, so it does not wait on one | Add `Result<TSuccess, TFailure>` — a caller cannot branch exhaustively on `AppError` today | SDK `Foundation/Results/` | `shapes/service/platform/responses/results.md` § *Typed failure* |
| ✅ N81 | **12 domain throws become `Result`** — 10 landed: `ClaimCheckPayloadRepository` (4, plus 2 `throw Missing(...)` helpers the `throw new` scan had missed) and `MigrationRunnerService` (5, scanner rows included). `Result<T>` gained `IsFailure(out error, out value)` along the way — the carrier has `Map` but no `Bind`, so propagating a failure through an async operation otherwise cost a cast per hop. Startup bridges back to a throw at `PostgresPersistenceStartupExtensions`, since a host has no result channel before its pipeline exists. `OAuth2TokenRepository.GetAccessTokenAsync` returns `Result<string>` and the `DelegatingHandler` bridges back, because the `HttpClient` pipeline reads only exceptions. **Two carve-outs close the row.** `DbUpBackgroundService:59` is an `IHostedService.StartAsync` — a host has no result channel before its pipeline exists, the same reason the Postgres startup path bridges back to a throw. `CloudEventsMessageSerializer:154` is blocked by the carrier, not by taste: `IMessageSerializer.Deserialize` returns `object?` and `Result<T>` constrains `T : notnull`, so converting it needs a redesigned serializer contract across every implementation and transport adapter — raised as `N91`, one type at a time. ✅ `ClaimCheckPayloadRepository` landed 2026-08-22 — `ReadAsync` returns `Result<byte[]>`, `Confine` returns `Result<string>`, `Missing` returns an `AppError`, and the filter bridges back at `ClaimCheck.cs:461` with `.Match(body => body, error => throw new ClaimCheckPayloadException(error.Message))`; 6 failure exits, not 4, because two were `throw Missing(...)` rather than `throw new`. Messaging suite 96/96. Scope filter — throws inside role-suffixed types only (`Service` · `Repository` · `Mapper` · `Adapter` · `Validator` · `Serializer`); 29 counted, 9 excluded as config-at-boot or programmer errors (`N82`), 7 excluded as argument guards and `NotSupportedException`. The 13, in landing order: `ClaimCheckPayloadRepository` 4 (`ClaimCheck.cs:223,242,291,295`) · `MigrationRunnerService` 5 (`:150,227,248` plus the 2 folded in by `N87`) · `DbUpBackgroundService` 1 (`:59`) · `OAuth2TokenRepository` 1 (`:102`) · `CloudEventsMessageSerializer` 1 (`:154`). `FluentValidationAdapter:52` is carved out — `ValidateAndThrow` is the declared throw half of the bridge, paired with `Validate` returning `ValidationError?`, so throwing is its contract rather than a missing `Result`. Re-scanned 2026-08-22 for throw-helpers (`throw Helper(...)`, the shape that hid two exits in the claim-check repository): none in the remaining types, so these counts stand. Each conversion changes a contract and every caller, so they land one type at a time with the suite green between them. Rides along: `ClaimCheckPayloadRepository`'s ctor parameter is still named `blobStorage` after `N86` | SDK | `results.md` § *What returns a `Result`* |
| ✅ N82 | the 9 config-at-boot and programmer-error throws stay exceptions and guards stay guards — the framework's own seams only catch what is thrown. The 9: `DbUpBackgroundService:33,38,41` · `MigrationRunnerService:129,181` (both gated on `MigrationOptions.AllowRollback`) · `OAuth2TokenRepository:66,71` · `ConfigurationMapper:42` · `EfInterceptorWiringValidator:109` | SDK | same |
| ✅ N69 | the failure mode decides, not the role — an operation with a failure mode wraps, one that cannot fail by construction returns bare. Supersedes `R3`, and opened by iteration 8 of the taxonomy analysis | SDK + products | same |
| N60 | Reopened 2026-09-09: `Web/ExceptionHandling/AppErrorProblemDetailsFactory.cs:10` still declares a static class; the prior completion marker had no landed interface. Move creation behind the replaceable interface required by `car-t-012`, update consumers, and verify the resulting mapping behavior | SDK | raised as `car-t-012`; conventions audit `BC02` |
| ✅ N91 | `IMessageSerializer.Deserialize` returns `Result<object>` across all 3 serializers and its 8 call sites. `object` satisfies `notnull`; the blocker was the null-for-empty return, and that null was already a hand-rolled one-case result — the doc said every caller converted it into an explicit failure by hand. Empty, malformed, and decoded-to-null are now `SerializationFailed`, so the seam is total and never throws: `TryReconstruct` runs outside the adapters' try/catch, where a throw stops the subscription rather than costing one message. Closes the last `N81` throw (`CloudEventsMessageSerializer:154`). Originally: `Deserialize` returned `object?`, which `Result<T>` cannot carry under its `T : notnull` constraint — decide whether the seam returns `Result<object>` plus an explicit empty case, or stays throw-based as a documented boundary. Blocks the last `N81` throw (`CloudEventsMessageSerializer:154`) | SDK `Messaging/Serialization/` | `shapes/service/platform/responses/results.md` § *What returns a `Result`* |

---

## Documentation

| # | Change | Where | Source |
|---|---|---|---|
| ✅ D6 · D3 | **[+]** Every `<remarks>` is at or under the 5-line cap, and `<para>` is gone — 0 of 181 multi-line blocks over cap, 143 `<para>` removed across 45 files. Run as three agent passes over 73 files: rewrite, then fix the 206 findings the verifiers raised, then close the 48 that survived. The passes converged 206 → 48 → 33, and the third introduced content regressions of its own, so the last 33 were finished by hand rather than a fourth pass. Three caller-facing facts restored after an agent cut them to satisfy the cap: Kafka's delivery count resetting to 1 on a redelivery whose offset was never stored, the `AddMessagingRecorder` idempotency note, and the inner exception on `ClaimCheckPayloadException` | SDK, `Messaging/` and `Testing/` carry most | `remarks.md:111` |
| ✅ D7 | **[+]** `<list>` markup gone from all 12 files — 15 `<item>`s flattened to `- ` bullets, wrappers dropped | SDK | `remarks.md:116` |
| ✅ N52 | 46 `<example>` tags stripped across 18 files — 36 single-line, 10 blocks; none remain | SDK | `core/lla/notation/documentation/inline.md:35` |

---

## Correctness

| # | Change | Where | Source |
|---|---|---|---|
| ✅ C3 | **[+]** Both `UseSerilogConventional` sinks render `{TraceId}` — Serilog 4.2.0 already populates `LogEvent.TraceId` from `Activity.Current`, so the gap was the output template, not the enrichment. No new seam: the console and file templates moved off the sink defaults, and the brackets render empty outside an activity. Verified against a real host — `[INF] [e9421149ccb8147aec30ca2876e6e37a] inside the request span` in the file sink, `[INF] [] outside any activity` beside it | SDK `Observability/Logging/` | `ideas/logging-analysis.md` § *Unused Serilog capability* |
| ✅ C5 | ~~`UseOwaspSecureHeaders` hard-codes COOP/COEP with no opt-out~~ — `SecureHeadersOptions` fills from an `Action<T>? configure = null` delegate, both flags default `true`, so an unconfigured host is byte-identical. `AddDefaultSecurityHeaders()` seeds COOP/COEP/CORP itself, so skipping the explicit `Add*` call would not have dropped the header — the opt-out removes the keyed entry from the `HeaderPolicyCollection`. `ApiDefaultsOptions` carries the same two flags and forwards them, mirroring how `AllowedHosts` reaches `ProxyAwareHostingOptions`. 4 tests in `Web.Tests/SecureHeaders/` assert both directions over a TestServer | SDK | `docs/backend-inventory.md` |
| ✅ C6 | The 4 null-forgiving lies are gone — each site now guards, retypes, or fails explicitly rather than asserting a non-null the code cannot promise | SDK | `ideas/nullability-and-fixtures-analysis.md` |
| ✅ C8 | An observing interceptor can no longer settle the message it watches — the receive and consume hooks take `EventEnvelope` instead of `ReceiveContext`, so `AcknowledgeAsync` and `DeadLetterAsync` are off the surface they are handed. The publish hooks already took the envelope. `Messaging.standard.md:88` said a registered observer must not change behaviour; the contract now enforces it rather than asking | A message observer can settle the message it is only supposed to watch. `EventProcessingPipeline.cs:55,122` hands `PreReceiveAsync` / `PreConsumeAsync` the live `ReceiveContext`, whose surface carries `AcknowledgeAsync` (`TransportContracts.cs:114`) and `DeadLetterAsync` (`:121`) — an observer calling either leaves the pipeline to ack again at `:58` or dead-letter at `:83`, so one message settles twice. `Messaging.standard.md:88` states the guarantee the code does not enforce: a registered observer must not change behaviour relative to none registered | SDK `Messaging/Transport/` | `Messaging.standard.md:88` |
| ✅ C9 | `Mediator.cs:60` now invokes with `BindingFlags.DoNotWrapExceptions`, so a synchronous throw reaches the mapper as itself and an `AppException(NotFound)` maps to its own status instead of 500 | `Mediator.cs:61` invokes the dispatcher through reflection without `BindingFlags.DoNotWrapExceptions`, so every synchronous pre-pipeline throw arrives as `TargetInvocationException`. `ExceptionMapper.Map` tests `exception is AppException` on the outer instance and never unwraps, so a handler constructor throwing `AppException(NotFound)` renders 500 instead of 404 | SDK `Mediator/` | `Foundation/Errors/ExceptionMapping.cs:39` |
| ✅ C10 | The converter can no longer throw from its own recovery — the mapper call is guarded and falls back to `Unexpected(inner:)`, and the observer call cannot preempt an already-built failure | `ExceptionToResultBehavior` can throw from inside its own recovery. `:36` calls `exceptionMapper.Map` and `:38` calls `observer.Record` inside the bare catch, both reaching app-registered seams — a throwing `IExceptionMappingRule` or `IErrorNatureClassifier` escapes the one component whose contract is never to throw, and replaces the original exception | SDK `Mediator/ExceptionHandling/` | `results.md` § *Throw and return bridge* |
| ✅ C11 | The bare catch excludes `NullReferenceException` · `ObjectDisposedException` · `StackOverflowException` · `OutOfMemoryException`, so a programmer error stays a throw, and the bridge back to a throw now carries the cause. A response with no failure arm keeps an `OperationCanceledException` as itself, which is what an awaiting caller expects | `ExceptionToResultBehavior.cs:34` catches bare `Exception` with no exclusion list, so `NullReferenceException` and `ObjectDisposedException` become failures — the standing rule keeps a programmer error as a throw. `:51` also calls `error.ToException()` with no `inner`, dropping the caught exception's type and stack | SDK `Mediator/ExceptionHandling/` | `core/mla/constructs/data/result.md` |
| ✅ C12 | `AddMediator` registers the converter first, so it is outermost by construction and every behaviour an app adds afterwards sits inside it. `AddMediator(o => o.ExceptionToResult = false)` opts a host out | The outermost-first ordering that the no-throw guarantee depends on is stated only in an XML doc line (`ExceptionToResultBehavior.cs:58`) and enforced nowhere. `Mediator.cs:68` reverses DI order, so registering `AddMediatorValidationBehavior` before the converter puts validation outside it and every `ValidationException` escapes | SDK `Mediator/` | `Mediator/mediator.md` |
| ✅ C13 | `AddMediatorExceptionToResultBehavior` brings both constructor dependencies plus `AddLogging()`, so a bare container resolves the behaviour at boot rather than failing at first dispatch | `AddMediatorExceptionToResultBehavior` registers `AddExceptionMapping()` for one constructor dependency and never `AddAppErrorObserver()` for the other, so a non-web host calling it alone fails at first dispatch rather than at boot — open-generic registrations skip `ValidateOnBuild` | SDK `Mediator/ExceptionHandling/` | `repo-structure.md` §3 also wants the missing `exception-handling.md` |

---

## Convention repair consequences — 2026-09-10

These rows follow the settled rules in the [conventions audit](../../../system/sessions/backend-beta-build/conventions-audit.md).
They distinguish verified source gaps from verification work; none is marked implemented by a documentation edit.
Explicitly deferred validation features remain deferred. Existing naming, options, error and lifetime obligations
remain in their original rows rather than being duplicated here.

Existing-row extensions: `N98` (interceptor vocabulary) must verify phase/job ordering against the corrected
definition; `ClaimCheckRehydratingConsumeInterceptor` currently differs. `N108` applies the newly documented
confirmed-role starters. `N111` uses the reconciled registration cases; `N94` (current user) retains per-save
scope safety; `N103` distinguishes binding faults from legitimate runtime misses. `N25` closed on 2026-09-13;
`N100` and `N101` vocabulary decisions are settled case by case; their source conformance remains open.
The already-accepted `Capabilities` baseline is applied.

P08 clarification (2026-09-13): N24's historical `{Root}JsonConstants` holder recommendation is superseded.
No per-type serialization wrapper or dedicated options-holder class remains justified by format customization.
Pass options or select a registered options profile through the shared serializer; C29 owns the missing shared
capability verification/completion. N24's shipped preset/EF work remains historical, not proof that keyed lookup ships.

Naming inventory refresh (2026-09-13): [current declarations and candidates](../../../system/sessions/backend-beta-build/sdk-naming-inventory.md).
Recorder decision: `InterceptorLog` → `InterceptorInvocationTracker` confirmed and applied to Data.Tests;
the recorder's role differs from the interceptors that call it. Analogous recorders renamed to
`RecordedMessageTracker` / `RecordedTransitionTracker`, including source/doc references. Old-name source
searches, scoped whitespace and Data.Tests / Testing.Messaging / Messaging.Tests builds pass; see
[verification](../../../system/sessions/backend-beta-build/recorder-rename-verification.md). The N100 row's
recorder-open wording above is historical; other suffix cases remain undecided. `ErrorNatureClassifier` →
`ErrorNatureMapper` applied with DI override retained; native escalation resolved the test-socket restriction.
`EventFaultPolicy` applied for configured Retry/DeadLetter/Ignore selection. All 28 existing scoped tests passed
([permission recovery](../../../system/sessions/backend-beta-build/permission-recovery.md)). Parser retained;
its construct/indexes are applied. SDK parser conformance remains in N100/N101, including separation of Cron's
`NextOccurrence` evaluation from syntax decoding; do not silently bless mixed responsibilities.
`DelayedRetryCoordinator` → `DelayedRetryService` applied; Messaging.Tests compiled and six existing fault/bus
tests passed through native approval ([evidence](../../../system/sessions/backend-beta-build/delayed-retry-service-verification.md)).
Its pre-existing options-registration callback concern remains under N111: inspect the singleton registration
inside `DelayedRetryServiceCollectionExtensions`' Configure callback; do not treat the rename as its repair.
This supersedes historical counts and premises in N100/N101. The 2026-09-15 refresh below closes the decisions. `InterceptorLog`
is in `Data.Tests`, not a shipped Testing package; the other two logs are shipped test helpers. Mapper permits
explicit failure, so that alone does not distinguish Parser. Naming source changes and their checks are recorded above.

Naming decision closure (2026-09-15): all cases are settled; no additional role permission is needed to
finish N100/N101. Retained roles: Parser, Exporter, Formatter, Transport, Serializer and Bus. Integrity checks
use Validator with a domain result when needed; input/field validation keeps the FluentValidation integration.
Google token evidence uses Authenticator. Guest-session orchestration and metrics recording use Service.
HumanizedTextFormatter keeps Formatter as its role. Internal wrappers use EventEnvelopeModel and
OtpDeliveryEnvelopeModel; the data-only HTTP test mirror uses TestApiResponse<T>. Payload events retain Event.
No separate Humanizer, Verifier, Session, Metrics or Envelope role was introduced. SDK rows N100/N101 remain
open until source conformance is verified, including Parser mixed responsibilities, new role folders/starters,
and the saga delivery wrapper's role. Current source/check evidence:
[naming inventory](../../../system/sessions/backend-beta-build/sdk-naming-inventory.md).

Additional source observations extend existing work: N108 reconciles hash-chain mixed-version documentation
with validation that always starts at sequence 1 and an empty prior hash; segment validation has no checkpoint
parameter. N101 verifies the Google authenticator's cancellation contract and get-only audience options against
the current method/options rules. Do not treat rename compilation as evidence these behaviors are repaired.

P04 extension (2026-09-13): `N108` also checks XML field admission while visiting each role. Inherit applicable
general/declaration-kind defaults when role fields or sections are omitted; retain explicit scoped bans and
the collaborator-only constructor exemption. Do not remove params/returns/qualifying remarks solely because
the role lists only a summary. This extends the existing documentation sweep, without claiming SDK source changes.

P06 extension (2026-09-13): `C21` also inventories request-handler composition and examples. Replace nested
command/query dispatch or direct-handler calls used for reuse with shared services; preserve required validation,
permission checks, cancellation and transaction ownership when removing the inner pipeline. Do not hide the
same dispatch behind a service or alter event publication contracts as part of this request-composition change.
Verify each affected operation's result and failure behavior; the convention edit is not evidence of SDK implementation.

P07 extension (2026-09-13): the `N108` documentation pass applies optional test-body comments. Keep descriptive
test names; AAA markers or a scenario/rationale comment are needed only when they clarify the body. Remove
requirements for a second gist and avoid redundant narration; preserve useful setup/provider/timing explanations.
Do not bulk-delete optional markers or treat missing markers as a failed convention check. Test XML exemptions remain separate.

| # | Change and closure evidence | Where | Source |
|---|---|---|---|
| N114 | Apply settled P01: retain sealed record entities; load-modify-save changes the original tracked instance. Verify same-instance saves preserve original concurrency values and property-level changes; do not call Set.Update merely to report already tracked mutations. Reject replacement copies while the key is tracked, without implicit merging/detaching/clearing. Copies remain candidates/snapshots; document any separately supported detached-update operation explicitly. Verify EF navigation reference membership and stable-key hash usage. No class conversion. | SDK entity declarations, EF repository/update lifecycle and affected consumers inside the SDK; inventory at implementation | Resolved P01 / BC09; `core/mla/constructs/data/entity.md`; `core/mla/domains/persistence/access/ef/ef.md#tracked-writes` |
| C14 | Enforce the existing HTTP string-enum contract in both directions, including undefined numeric values and flags; add the missing ISO-duration wire converter. Verify the full controller preset for dictionary casing, null omission and scalar round trips. Preserve an independent persisted-JSON contract; do not globally replace storage converters. | SDK `Web/Json/` and relevant serialization tests | `BC16`; `shapes/service/platform/responses/serialization.md` |
| C15 | Verify `UseApiDefaults` ordering when limiter partitions or cache policies depend on identity. Its current bundle runs limiter/cache before caller-added authentication; provide a supported composition seam where needed. Exercise routing metadata, authorization and compression in a real host. | SDK `Meta/` and `Web/` | `BC17`; `shapes/service/platform/startup/host-configuration.md` |
| C16 | Fix the unconditional packaging group overriding test `IsPackable=false`. Evaluate every actual test project and shipped testing library after all imports. Align compiler/SDK selection with an explicit supported pin and roll-forward policy. | `src/Directory.Build.props`, SDK selection and CI inputs | `BC14`, `BC24`; `shapes/sdk/build/build.md` |
| C17 | Reconcile release metadata and stale repo instructions with actual project/workflow state. Record revision, evaluated version and all seven current pack outputs; verify assets, family dependencies and test-dependency isolation. Current workflow has no test step; required verification must describe the actual published revision and version, respecting any explicit repository override. Preserve workflow-owned version increment; do not double-bump. | `CLAUDE.md`, architecture/registry docs, `.github/workflows/publish.yml`, package metadata | `BC02`, `BC24`; `shapes/sdk/delivery/delivery.md` |
| C18 | Make SDK test-host clocks coherent for both `TimeProvider` and NodaTime `IClock`; currently replacing the former leaves the latter independent. Eliminate process-global environment mutation in multi-host test setup. Verify two simultaneous hosts with independent time/configuration and restoration of prior state. | SDK `Foundation/Time/`, `Testing/`, `Testing.Data/` | `BC15`; `core/mla/components/time.md` |
| C19 | Verify each migration provider's actual coordination boundary; a journal alone is not a mutex. Cover two competing runners, rollback limits, interrupted nontransactional index recovery, enum add/use commit boundaries and SQLite constraint evolution. Verify CLI result-kind exit codes `0/1/2`. Verify driver and EF enum registration together with multi-word-label insert/read. Implement only missing SDK guarantees; keep deployment coordination explicit when provider-owned. | SDK `Data/` and migration tests | `BC10`–`BC12`; persistence migration owners |
| C20 | Align JWT helper enforcement and documentation with one intended key source and secure remote metadata defaults. Current helper accepts simultaneous sources, derives HTTPS requirement from URI scheme and treats `JwksUri` as metadata address. Keep any development escape explicit; verify failure cases and algorithm/key requirements. | SDK `Identity/Jwt/` | `BC18`; `core/mla/domains/identity/jwt/jwt-auth.md` |
| C21 | Verify declared validation phase/order and error-code behavior against current registrations and adapters. Apply external model validation by default (pure extension or dedicated FluentValidation-backed validator); constructor data checks require an exceptional documented type contract. Validate copied/deserialized candidates at the accepting boundary, preserving programmer guards. Preserve the synchronous authored validator contract; keep async/ruleset/read-seam work at its explicit deferral trigger, recording blockers rather than silently expanding the feature release. | SDK `Foundation/Validation/` and mediator validation | `BC05`, resolved P02; `core/mla/domains/validation/validation.md` |
| C22 | Recheck logging/tracing against the recovered research and current sources. Correct `AddOtlpExporters` documentation claiming log export when its body wires traces and metrics only. Verify module sources/meters, correlation, bounded tags, single boundary error recording, disabled listeners and exporter failure isolation. Extend completed `C3` evidence without repeating its fixed TraceId-template work. | SDK `Observability/`, instrumented operation boundaries | `BC23`; existing `car-t-008`, `car-t-011`; observability domain |
| C23 | Provide durable startup-failure reporting before host construction and final logger configuration. Capture creation/binding/validation/start failures independently of application DI/logs; preserve original exception and nonzero exit, flush the sink, verify persisted output after child-process failure. Current `UseSerilogConventional` callback alone cannot cover earlier failures. | SDK host/bootstrap logging seam | `BC23`; existing `car-t-014`; `core/mla/domains/observability/serilog/serilog.md#startup` |
| C24 | Verify outbound retry and hedging against side effects, cancellation and total budgets; `HttpResilienceOptions` currently has no operation replay-safety selector. Supply a supported safe composition where needed. Cover non-replayable bodies and disposal of responses/streams. | SDK `Http/` | `BC25`; `core/mla/domains/integrations/http/http.md` |
| C25 | Verify existing tenant and messaging seams against the clarified contracts: authoritative tenant read/write scope, cancellation/resource lifetime, at-least-once delivery, atomic dedupe/effect, outbox, bounded retry/deadletter and backpressure. Record provider-specific evidence or missing guarantees; a tenant marker or registration call is not proof. Preserve deferred feature scope. | SDK tenancy, persistence and messaging boundaries | `BC25`; entity-contract and messaging owners |

| C26 | Complete exact-package deep-copy verification and integrate FastCloner, the confirmed library for owned, detached data graphs. Exact-package verification passed: native escalation restored FastCloner 3.5.6 from official NuGet; all 32 reflection graph-copy checks passed on .NET 10.0.8 Arm64. SDK dependency/seam integration remains open. Require runtime subtype/private/init/get-only copying, cycles/shared aliases, original isolation, comparer preservation and usable cloned hash keys (including record keys with reference members). Exclude live resources and EF contexts/proxies; do not infer NativeAOT support or validation from cloning. Preserve explicit shallow `with` usage and copied entity identity. | Shared SDK data-copy seam and dependency pin; inventory placement before implementation | Developer confirmation 2026-09-12; `core/mla/constructs/patterns/prototype.md`; workspace `system/sessions/backend-beta-build/deep-copy-analysis.md` |

| C27 | Inventory SDK value objects whose generated equality does not match their value contract, especially collection members. Retain generated equality when correct; implement explicit typed equality and matching hashing when contents define the value. Document order/duplicate semantics and non-value member exclusions; verify separate equal instances, differing contents and equal hashes. Preserve hash-input stability; cloning is not equality or immutability. Do not alter unrelated entity identity semantics. | SDK value-object declarations and existing equality/comparer support; inventory at implementation | Confirmed P03 / BC09; `core/mla/components/value-object.md#equality` |

| C28 | Inventory API request mappings and SDK examples. Keep each dedicated `{RequestType}Extensions` companion in `{RequestType}.cs`, with its request's namespace/folder; preserve unrelated extension grouping. Limit mapping to simple deterministic body/route/caller projection, with no I/O or business workflow. Apply the scoped receiver-name and file exception in convention checks; do not split these companions as violations. If no applicable SDK declarations exist, record that evidence rather than create unused types. | SDK API request declarations, mapping examples and convention checks; inventory at implementation | Confirmed P05 / BC07; `core/mla/domains/api/api-messages.md#mapping` |
| C29 | Complete the shared serialization path for explicit options or selection of a registered JSON-options profile by key; no product per-type serializer wrappers or dedicated options-holder classes. Current source inventory finds explicit options on the message serializer and EF converters, but no general keyed JSON-options registry. Verify the shared abstraction and implement the missing capability; retain independent HTTP/stored contracts, consistent read/write/comparison options, explicit unknown-key behavior and existing absence/error semantics. Update examples and N24 product handoff to remove the old holder recommendation. Foundation stored-options example already changed to a scoped options value; runtime implementation and verification remain open. | SDK Foundation serialization abstraction/registration, EF integration and affected examples | P08 clarification; `core/mla/domains/persistence/stored-json.md` |

## Historical phantom-symbol findings

The following is the original snapshot, retained as history. Active convention examples were repaired in
the 2026-09-10 convention pass; resume source work from the current row and current convention owner.

- ~~`AddEnvironmentOverrides` · `AddIntegrations` · `AddPipelines` · `AddSchedulers` · `AddObservers`~~ —
  **cut 2026-08-19.** The table that held them was the stale horizontal model; the doc now documents only
  methods a host declares. `hosted-service.md:82` still cites `AddSchedulers()` — repoint it to `AddCodes()`-style
  domain wiring.
- `IErrorMessageMapper` / `IFieldErrorMessageMapper` — the docs are **right** and the source is wrong:
  it ships as `…Resolver`, and `Resolver` is a folded suffix. Rows N12 and N13 rename the source.

---

## Open

- ~~whether the 5 phantom `Add*` methods were a plan or a mistake~~ — cut, they were the horizontal model's residue
