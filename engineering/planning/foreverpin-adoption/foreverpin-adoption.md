# ForeverPin SDK adoption sweep

*Last updated: 2026-09-26*

> Full backend component inventory for one coordinated SDK improvement and ForeverPin upgrade cut.
> This owns the SDK-facing sweep; product UX and unresolved delivery choices remain in ForeverPin's product tracks.

## Status

- [x] Inventory both hosts, all 12 projects and all 215 authored C# files.
- [x] Trace SDK imports and product-owned infrastructure against current SDK source.
- [x] Reproduce four SDK boundary defects in an isolated executable.
- [x] Improve and test publication verification; published in `10.0.57-beta` from `a645d1b`.
- [x] Implement the autonomous SDK cut with focused regressions; product-only rows remain below.
- [x] Verify the complete candidate family and prepare the developer-owned release.
- [x] Publish the candidate: `ba2a87e` released as `10.0.58-beta`; all seven packages verified on NuGet.
- [x] Apply the authorized product adoption cut across both hosts and all 12 backend projects.
- [x] Complete final product regressions against the local SDK candidate: 231 passed, zero skipped.
- [ ] Publish the adoption-discovered SDK corrections and repin ForeverPin to the verified release.
- [ ] Run the published-package backend verifier; frontend acceptance belongs to its separate lane.

The user authorized deep implementation where the answer follows from current contracts.
Work requiring new product or platform policy is deferred explicitly below.
The original `10.0.58-beta` cut is published. Adoption exposed two additional SDK corrections, now locally packaged;
publication and the final consumer pin remain open. Commit permission is enforced by each repository's native switch.
Product evidence: [backend adoption](../../../../../ventures/10x-venture-forever-pin/engineering/architecture/backend-adoption.md).

---

## Original baseline

- SDK source baseline: `b01f6b6`; CI released `10.0.56-beta` at `a7f98013abfc6b7fb4c1ea2bbc08b228297726ea`.
  The peeled remote tag matches the revision recorded by the workflow's pack and package-verification steps.
- [Publish run](https://github.com/wow-two-sdk-beta/wow-two-sdk.backend.beta/actions/runs/35426464818):
  build, tests, packing, NuGet push and release tag succeeded; the propagation check timed out.
- All seven package URLs returned HTTP 200 during this sweep, including the new verifier's live run.
- ForeverPin references runtime `10.0.45-beta` and testing `10.0.40-beta`.
- The shared ForeverPin checkout has another lane's solution/verification changes; preserve its index and worktree.
- The prior package probe's 29 compilation errors were the first failing layers, not an exhaustive migration list.
  Source review also finds current-user/guest names, style serialization, error factories and fixture API changes.
- The first published adoption cut was `10.0.58-beta`: candidate commit `ba2a87e`, release commit `546466e`,
  annotated tag `v10.0.58-beta` peeling to `546466e`. The final consumer release must also include the later
  optional-calendar-end and test-host corrections recorded below.

Product file names below resolve under ForeverPin's `engineering/codebase/forever-pin.backend-services/`.
SDK source paths resolve under `engineering/codebase/wow-two-back-beta-sdk/src/`.

---

## Ownership inventory

| Area | Existing product seam | SDK ownership and disposition |
|---|---|---|
| Host floor | Both `HostConfiguration` partials and `Program` files | Keep product composition; adopt `ApiDefaults`, startup diagnostics and identity callback. |
| Configuration | `ApiSettings`, `AuthSettings`, `BillingSettings`, `RedirectSettings` | Keep product settings; use `ConfigurationMapper` and validate enabled capabilities. |
| Observability | SDK defaults, handler loggers, scan-flush logger | SDK owns logging/traces/error recording; product owns business event labels and scan metrics. |
| Errors / HTTP | `ControllerProblemExtensions`, broad handler catches | SDK owns exception classification and ProblemDetails factory; product owns its error catalog. |
| Mediator / results | Commands, queries, `AppResult`, success wrappers | Keep product requests and results; consume the SDK pipeline without nested dispatch. |
| Validation | Content, rule, rule-set and command validators | Keep business rules; SDK owns adapters, aggregation and field-error mapping. |
| JSON union registration | Local `SubtypeRegistry` and JSON extension | Remove duplicate implementations; use SDK registry and modifier. |
| Stored JSON | `JsonbOptions`, `CodeContentJson`, `CodeRuleJson`, SDK `StyleSpecJson` calls | Remove per-type wrappers; use immutable SDK profiles and direct serialization. |
| EF JSON tracking | `CodeEntityConfiguration` converter/comparer | Use SDK `HasJsonConversion`; verify full graph snapshot and old data compatibility. |
| Entities / repositories | Four entities; code, user and subscription repositories | Keep domain types and owner-scoped queries; SDK owns traits/audit/common persistence mechanics. |
| Migrations | SQL resources, startup apply and migration harness | Keep product SQL; adopt SDK result-returning runner and updated fixtures. |
| Transactions / concurrency | Count/check/write, guest claiming, webhook upsert, scan flush | SDK owns generic transaction mechanics; product owns invariant scope and retry decisions. |
| Guest identity | Raw `user-id` cookie resolves the owner ID | SDK must authenticate the guest capability; product owns guest-to-account transfer. |
| Registered identity | Google sign-in, cookie issuance, claims | Adopt `IGoogleIdTokenAuthenticator` and renamed services; retain product account projection. |
| CSRF / browser auth | Cookie-authenticated mutations and Google sign-in | Reusable protection belongs in SDK; product wires endpoints and browser token handling. |
| Code rendering | `CodeImageService`, SDK QR/barcode engine | SDK owns render safety and image format guarantees; product maps mode/rules to payload. |
| Payload serialization | Wi-Fi, mail, SMS, phone, geo, vCard and calendar encoders | Generic protocol encoding belongs in SDK; product content union and routing modes remain local. |
| Short IDs | `SlugGenerator` | Generic cryptographic generation belongs in SDK; length and collision retries remain product choices. |
| Routing / request context | `RoutingService`, UA mapper, language parser, direct UTC reads | Keep rule policy; reusable UA/language/time primitives belong in SDK. |
| Geo | `NoopGeoBroker` | IP geolocation is a future SDK adapter; provider/database operations require a decision. |
| Caching | Unwired `CachedRedirectCodeRepository`; active DB repository | SDK already has cache seams; preserve fresh reads until invalidation semantics are settled. |
| Analytics / background work | Bounded channel and scan-flush worker | Keep scan models/projection; reusable queue/lifecycle mechanics may move into SDK. |
| Billing / outbound I/O | `StripeBillingBroker`, checkout/portal/webhook handlers | Stripe protocol mechanics belong in SDK; plans, entitlements and fulfillment remain product policy. |
| Testing | Unit, repository, multi-host and migration suites | SDK owns generic fixtures and engine tests; product owns stored-format and endpoint regressions. |
| Build / dependencies | Repeated SDK pins and floating package versions | Align one family version, stabilize build inputs and remove unused references after evaluation. |
| Localization | English validation/error messages; routing language parsing | SDK request culture and mapper seams exist; full translation remains a separate vector. |
| Unused vectors | Messaging, jobs, storage, AI, tenancy, realtime, generic outbound HTTP | No current direct product use was found; do not add registrations solely because an SDK API exists. |

No blanket repository replacement is justified: `GetByIdForUserAsync`, subscription identity and guest claiming carry
product policy that a generic CRUD API does not replace. `ContentMode`, `CodeRuleType`, plans and product response shapes
also stay in the product. A folder named `Common` alone is not evidence that its contents belong in the SDK.

---

## Autonomous implementation cut

The following work is authorized and does not need another naming or design vote. Checkboxes are execution state,
not a request for confirmation. Apply SDK corrections first and consume them through one published package cut.

### A01 — CI publication verification

- [x] Poll only unavailable packages under one 15-minute deadline, with bounded individual requests.
- [x] Print each unavailable ID, HTTP status and curl status; list all unverified IDs on timeout.
- [x] Verify the annotated remote tag resolves to the exact tested release revision.
- [x] Cover immediate success, delayed propagation, transport recovery, timeout, slow requests and bad arguments.
- [x] Run the actual verifier against all seven `10.0.56-beta` packages.
- Do not rerun the whole publisher merely to recheck NuGet visibility: that creates a new version.

### A02 — Complete API migration inventory

- [x] Consume `ICodeRenderer.Render` as `Result<RenderedCode>` and map its validation error at the HTTP boundary.
- [x] Replace manually constructed renderers with SDK registration or pass the new rasterizer collaborator.
- [x] Configure persistent/shared Data Protection keys for intended guest-sharing hosts; legacy raw guest cookies are rejected.

- [x] Centralize runtime and testing references under one version property.
- [ ] Set that property to the verified published version containing the adoption-discovered corrections.
- [x] Migrate `ConfigurationLoader` to `ConfigurationMapper`.
- [x] Migrate `IGoogleIdTokenVerifier` / registration / fake to the authenticator contract.
- [x] Migrate `ICurrentUser` and `IGuestSession` to `ICurrentUserService` and `IGuestSessionService`.
- [x] Migrate `StyleSpecNormalizer` to `StyleSpecMapper`.
- [x] Replace static ProblemDetails creation and `IErrorMessageResolver` with `IAppErrorProblemDetailsFactory`.
- [x] Replace migration exception assertions with `Result<T>` success/failure assertions.
- [x] Replace removed `MultiHostFixture.ConfigureEnvironment` with host-local configuration hooks.
- Acceptance: every backend project compiles, including the management host and test harnesses previously hidden
  behind upstream compiler failures. Update the host-local connection, redirect URL and fake-service registrations.

### A03 — Error classification and safe responses

- [x] Teach SDK database mapping to classify EF-wrapped provider failures while preserving the outer diagnostic cause.
- [x] Remove product broad catches that convert unexpected exceptions and cancellation into `Unexpected(ex.Message)`.
- [x] Keep deliberately handled business failures typed and safe; scope webhook signature handling narrowly.
- [x] Render product failures through the registered SDK ProblemDetails factory, preserving field paths and headers.
- [x] Move the generic controller-to-ProblemDetails adapter into the SDK; no product-specific logic belongs in it.
- Reproduced: direct PostgreSQL `23505` maps to `Conflict`; the same error inside `DbUpdateException` is unmapped.
  Removing product catches alone will therefore not establish correct 409 behavior.
- Acceptance: real PostgreSQL unique violation through HTTP gives 409; unexpected failures expose no database/provider
  text; request cancellation is not reclassified as a business failure; each unexpected failure is recorded once.

### A04 — JSON consolidation and persisted-contract compatibility

- [x] Harden SDK subtype registration: decoded JSON tokens, unique discriminators, declared kinds, closed/non-null types and read-only entries.
- [x] Verify stored JSON snapshots isolate nested mutation and retain equality across serialization.

- [x] Remove the local generic registry, modifier and `JsonbOptions` duplicates.
- [x] Register product union profiles explicitly in both hosts and provider-specific test contexts.
- [x] Replace `CodeContentJson`, `CodeRuleJson` and removed `StyleSpecJson` calls with direct SDK-backed serialization.
- [x] Replace the custom shallow EF comparer/converter with `HasJsonConversion` under the stored rule profile.
- [x] Add corpus tests for every content/rule subtype, reordered jsonb discriminators and legacy style documents.
- Preserve the accepting boundary's established absence behavior: content null/blank → absent; rules null/blank → empty.
- Style tests currently promise null/blank/`{}`/malformed → default. Raw `JsonSerializer.Deserialize<StyleSpec>` does not
  preserve that behavior; keep the default/recovery policy at the stored-style read boundary, without a wrapper role.
- Do not silently change stored ECC tokens: current product tests expect `"H"`; the generic stored preset uses
  camelCase enum strings. Test reading old values and define the explicit stored profile before changing writes.
- EF model metadata is cached: do not capture a scoped provider or mutable per-request serializer into the model.

### A05 — Rendering guarantees and input safety

- [x] Make the SDK facade return PNG for every supported barcode when PNG is requested.
- [x] Reject unsupported format/symbology values rather than silently selecting SVG or Code128.
- [x] Escape every caller-provided SVG attribute, including colors and gradient-stop colors.
- [x] Add external style/request validation for finite numeric values, bounded dimensions and supported data images.
- [x] Bound raster allocation and handle invalid SVG/native allocation failure as controlled failures.
- [x] Carry generic renderer/normalizer/shape/decode tests into the SDK; keep product payload and HTTP tests local.
- Original baseline reproduced: Code128 + PNG returns `Format=Svg` and `image/svg+xml`.
- Original baseline reproduced: a quote in `ForegroundColor` injects a separate attribute into the emitted SVG.
  This proves an output-encoding defect; browser script execution and external resource fetching were not tested.
- Acceptance: PNG signatures and MIME types agree; SVG remains valid XML under hostile strings; non-finite or enormous
  inputs are rejected before native rendering; valid preview/saved-image parity and decode coverage remain intact.

### A06 — Validation at product entry points

- [x] Validate preview input before rendering; it currently bypasses mediator validation.
- [x] Reject null collections/content/style and undefined enum values at the HTTP boundary.
- [x] Load the owner-scoped entity before mode-dependent update validation; use its persisted mode.
- [x] Reuse product validators through SDK adapters while preserving `Rules[i].Content.*` failure paths.
- [x] Validate condition values as device/country/language/time-window data rather than only nonempty strings.
- Keep input validation external. The loaded-data phase must not introduce nested request dispatch.
- Acceptance: malformed preview returns 400; a static update cannot acquire multiple rules; unauthorized/absent targets
  retain the product's owner-scoped response contract; failed validation leaves the row unchanged.

### A07 — Guest capability correctness

- [x] Replace SDK raw-GUID cookie trust with an authenticated guest capability, shared by provisioning and resolution.
- [x] Reject tampered/raw values and isolate the guest capability's purpose from registered account authentication.
- [x] Clear the request's resolved guest state consistently when the guest session is cleared.
- [x] Require a valid guest capability for product guest-to-account reassignment.
- Reproduced: setting an arbitrary GUID in `user-id` makes `CookieCurrentUserService` return that owner as a guest.
  Product repositories scope directly by this ID; sign-in may reassign all rows owned by that supplied ID.
- Acceptance: forged account/guest IDs cannot read, mutate or claim rows; valid same-device guest claiming still works.
- A legacy raw-cookie fallback would preserve the defect. If preservation of real existing guest sessions becomes
  necessary, pause only that rollout policy; the secure codec and regression tests can be implemented independently.

### A08 — Host composition, settings and clocks

- [x] Apply settled subject names: `AddPostgresDatabase`, `AddCodes`, `AddMediator`, `AddRouting`.
- [x] Put identity middleware in the SDK's `UseApiDefaults` callback.
- [x] Use startup diagnostics for both hosts and validate settings for explicitly enabled external capabilities.
- [x] Replace inert `Logging:LogLevel` with the Serilog configuration actually consumed.
- [x] Inject `TimeProvider` into redirect request context and tests instead of reading `DateTimeOffset.UtcNow` directly.
- Acceptance: both hosts boot; identity precedes identity-sensitive policies; one fake clock controls routing/audit tests.

### A09 — Bounded correctness work in data and background processing

- [x] Keep tracked entity mutation; reject accidental detached replacement writes rather than silently doing nothing.
- [x] Make one scan flush atomically insert events and update their counters using the existing EF transaction API.
- [x] Add shutdown/drain and drop/failure observability to the existing bounded scan queue.
- [x] Add SDK unbiased cryptographic ID generation with caller-owned length/alphabet.
- [x] Adopt the generator in the product; bound collision retries and rely on the database unique constraint.
- The original product generator reduced bytes modulo 62; the adopted SDK generator uses unbiased selection.
- Do not invent a universal data-session abstraction to fix one EF-only transaction.
- Acceptance: failed scan flush cannot leave events/counters inconsistent; cancellation does not masquerade as success;
  a forced slug collision cannot spin indefinitely or overwrite an existing code.
- Plan caps, concurrent subscription upserts and guest-transfer transaction scope need explicit concurrency tests.
  Shared infrastructure may support them; entitlement and conflict-resolution policy remain product-owned.

### A10 — Reusable payload and request-context primitives

- [x] Extract protocol encoding from product-specific content records into typed SDK serializers, retaining the union locally.
- [x] Preserve the current valid payload corpus before changing protocol details; add boundary/escaping/culture tests.
- [x] Fix the known HTTP destination path to read the URL from URL/mobile-app content rather than calling `Encode()`.
- [x] Extract reusable User-Agent classification and preference-aware language parsing; retain product rule evaluation.
- Original source evidence: URL and mobile-app `Encode()` returned null while `RoutingService.Resolve` called it.
  Dynamic routing fell through to `NotFound`; adoption now reads the destination URL and has product regressions.
- SDK `FloatingCalendarEventExporter` explicitly accepts timezone-free `LocalDateTime`; it does not infer timezone policy.
- Protocol corrections are tested and recorded in `Codes/Payloads/Payloads.spec.md`; the product corpus changes during adoption.
- Calendar timezone semantics and non-HTTP delivery remain deferred, not guessed during generic extraction.
- Acceptance: URL/mobile-app destinations resolve; valid existing payloads retain agreed semantics; accepted language
  weights/exclusions and invariant time-window parsing have explicit tests.

### A11 — Testing and build reproducibility

- [x] Centralize family versions; evaluate direct package usage before removing duplicate/transitive references.
- [x] Pin currently floating build dependencies and the .NET SDK to a verified compatible baseline.
- [x] Add shared build settings consistent with the service convention; run all resulting diagnostics.
- [x] Replace global environment mutations and the obsolete test-provider comment with instance-owned fixture settings.
- [x] Ensure test overrides preserve audit/other production interceptors.
- [x] Keep generic migration engine tests in SDK; retain product migration resources and host startup coverage locally.
- Nullable is already enabled in the current projects. The old handoff claim that no `Directory.Build.props` means
  nullable warnings cannot surface is refuted; the gap is centralized reproducible policy, not absent nullable analysis.
- Do not rename or restage another lane's solution/verification files during this cut.

### A12 — API/application convention adoption

- [x] Apply domain-first API placement and settled `*Dto` edge / `*Model` application naming.
- [x] Keep trivial request mapping in its request file; retain actual product orchestration services.
- [x] Move roles such as background workers and extensions to their agreed folders.
- [x] Reconcile the old naming/documentation rows against current source rather than replaying the historical counts.
- These are product changes in the coordinated upgrade, not reasons to grow new SDK wrappers.

---

## Deferred decisions

These items require a new contract, operational choice or product policy. They do not block independent corrections
above, and they are not deferred merely because their implementation is long.

| ID | Decision / existing owner | Safe boundary for this cut |
|---|---|---|
| D01 | SDK `IDataSession` source is implemented in the later, unpublished data-pipeline cut | Product invariants use its existing shared EF context and transactions; no second session abstraction is needed for this adoption. |
| D02 | Cache invalidation across management and redirect hosts; freshness and deployment topology | Keep the active uncached DB path; SDK cache availability alone does not justify wiring stale reads. |
| D03 | Dynamic non-HTTP delivery, resolve-page host, geo behavior and calendar timezone meaning; product delivery policy | Fix already-defined URL routing; preserve/defer the remaining delivery choices. |
| D04 | Full payments adapter and webhook state/replay/order model; Stripe target is currently LATER | Keep product plans local; map provider failures safely; no speculative generic billing framework. |
| D05 | IP geolocation provider, database licensing/update process and proxy trust boundary | Do not claim country routing works while `NoopGeoBroker` is registered. |
| D06 | Complete translation catalogs, culture/fallback policy and error-code governance | Adopt existing field-error/message seams; retain current English contract. |
| D07 | Full identity rebuild, guest account separation/recovery and browser CSRF contract | Close raw guest-cookie trust independently; coordinate any browser protocol change with the frontend lane. |
| D08 | Durable analytics, retention/privacy requirements and queue loss guarantees | Make the current best-effort flush atomic/observable; do not silently promise durable delivery. |
| D09 | Barcode styling scope, PDF/print output, per-eye styling and richer logo options | Honor existing SVG/PNG requests and validate inputs; new visual/output policy stays in its vector. |

Further findings can extend these subjects; do not create a new discussion for an already-settled name or mechanical fix.

---

## Verification and commit handoff

### Product adoption candidate — 2026-09-26

- SDK source base: `94b32a9fb71a703c52d82f5ab67e74b238159cfe`; HTTP/data/idempotency follow-up commits are local.
- Adoption corrections committed: `f84aeba` makes `FloatingCalendarEventModel.End` optional;
  `c40fd57` makes `WebApiTestHost` default to `Development` so dependency lifetime validation remains enabled.
  Neither correction is in published `10.0.58-beta`.
- Full SDK Release run before the test-host correction: 610 passed, one existing Kafka skip.
  The affected Web suite was rerun after that correction: 103 passed, zero skipped.
- All seven `10.0.59-beta.local.20260926.2` package/symbol pairs passed artifact verification under
  `/tmp/foreverpin-sdk-candidate-20260926-2`. Their repository metadata identifies the base revision;
  these working-tree artifacts do not claim a committed or published snapshot.
- ForeverPin's complete final candidate run passed: 81 HTTP, 30 PostgreSQL integration, one product migration
  and 119 unit tests; 231 passed, zero skipped. Evidence: `/tmp/foreverpin-adoption-tests-final.log`.
  The run includes deep JSON tracking, owner-transfer races, field-path validation and both host environments.
- Both runtime and test package references use `BackendSdkVersion`. The temporary checked-in default is
  `10.0.58-beta`; local tests explicitly override it. The product must not be committed with that temporary pin.
- Publish the SDK through the normal developer-owned main push, verify all seven resulting packages,
  replace the product pin with that actual version, and run the backend verifier without local overrides.
- Frontend/browser acceptance and provider-backed production verification remain separate.

### CI batch — published

- `python3 scripts/test-verify-published-packages.py`: six tests passed.
- `bash -n scripts/verify-published-packages.sh`: passed.
- Workflow YAML parsed successfully with Ruby's YAML parser.
- Every workflow shell step passed `bash -n`; the live peeled tag matches the package-verification revision.
- `bash scripts/verify-published-packages.sh 10.0.56-beta 60 5`: all seven packages confirmed available.
- `git diff --check`: passed for the CI change.
- CI correction committed as `a645d1b`; release commit `66aceb2` carries `10.0.57-beta`.
- [Publish run 35428036926](https://github.com/wow-two-sdk-beta/wow-two-sdk.backend.beta/actions/runs/35428036926)
  completed successfully, including the new bounded propagation check.

### SDK candidate — 2026-09-19

- Full Release solution built successfully; no new package dependencies or project files were required.
- All seven package/symbol pairs passed `verify-release-packages.sh`, including assembly assets, dependency family,
  version and repository metadata. Local artifacts: `/tmp/sdk-adoption-pack-3trb__zs`.
- These are uncommitted working-tree packages using the existing `10.0.57-beta` build version and baseline metadata,
  not a published `10.0.58-beta` or a claim that `66aceb2` contains these edits. CI rebuilds the developer's committed revision.
- The three test-companion packs completed with `-m:1`; default worker fan-out stalled locally. CI pack commands and
  SDK build guidance now use the same single-worker setting. Actual artifact verification passed after the retry.
- All seven suites executed: **552 passed, one existing Kafka dead-letter skip**.

| Suite | Passed | Skipped |
|---|---:|---:|
| Data | 28 | 0 |
| Foundation | 204 | 0 |
| Identity | 25 | 0 |
| Mediator | 69 | 0 |
| Messaging | 124 | 1 |
| Migrations | 26 | 0 |
| Web | 76 | 0 |

- Rendering regressions decode PNG output for all seven symbologies. The transferred QR corpus covers exact default SVG
  parity, module/finder geometry, gradients, transparent backgrounds and center emoji.
- Security regressions reject raw/tampered/expired/wrong-purpose/wrong-key guest cookies and unsafe SVG/image inputs.
  Provision/clear/reprovision agree with current-user resolution inside the same request.
- Serialization regressions cover nested JSON snapshots, escaped/duplicate subtype tokens, protocol corpus, Unicode folding,
  invariant coordinates and weighted language preferences. A custom HTTP factory proves controller response replacement.
- Full suite required native execution after sandbox `SocketException (13)`; the authorized retry completed successfully.
- Mechanical convention sweep reports no folded/banned role, bare IEntity, para-tag or severity-glyph hits.
  Static-form hits are the existing permitted factories; new behavior implementations use instance roles.
- Breaking adoption requirements are recorded under A02.
- Committed as `ba2a87e` (`feat: completed breaking SDK fixes for ForeverPin adoption`).
- [Publish run 35442850653](https://github.com/wow-two-sdk-beta/wow-two-sdk.backend.beta/actions/runs/35442850653)
  reran the same seven suites on that commit: 552 passed, one Kafka skip. It released `10.0.58-beta`.
- Deferred D01–D09 remain decisions, not partially implemented platform guarantees.

### Original baseline reproductions — corrected in this candidate

An isolated console probe compiled the pre-fix source of `DbExceptionMappingRule`, `CodeRenderer`, `SvgRenderer` and
`CookieCurrentUserService` against existing SDK build dependencies. It did not change product files or contact providers.

```text
Direct unique violation: Conflict
EF-wrapped unique violation: unmapped
Requested barcode PNG: Svg, image/svg+xml
Color injected a new SVG attribute: True
Unsigned caller-selected ID accepted as guest: True
```

These baseline observations received passing SDK regressions in the original cut. The later product adoption
and its verification are recorded in the 2026-09-26 candidate section above.

### Source pointers

- Error wrapping: [database mapping rule](../../codebase/wow-two-back-beta-sdk/src/Data/Errors/DbExceptionMappingRule.cs).
- Format dispatch: [code renderer](../../codebase/wow-two-back-beta-sdk/src/Codes/Rendering/CodeRenderer.cs).
- SVG attributes: [SVG renderer](../../codebase/wow-two-back-beta-sdk/src/Codes/Rendering/Svg/SvgRenderer.cs).
- Guest trust: [current-user service](../../codebase/wow-two-back-beta-sdk/src/Identity/CurrentUser/CookieCurrentUserService.cs).
- Stored profiles: [profile registration](../../codebase/wow-two-back-beta-sdk/src/Foundation/Serialization/StoredJsonOptionsProfileExtensions.cs).
- Product JSON tracking: [code mapping](../../../../../ventures/10x-venture-forever-pin/engineering/codebase/forever-pin.backend-services/ForeverPin.Persistence/Configurations/CodeEntityConfiguration.cs).
- Update validation: [update validator](../../../../../ventures/10x-venture-forever-pin/engineering/codebase/forever-pin.backend-services/ForeverPin.Application/Codes/Core/Validators/CodeUpdateModelValidator.cs).
- Preview bypass: [codes controller](../../../../../ventures/10x-venture-forever-pin/engineering/codebase/forever-pin.backend-services/ForeverPin.Api/Codes/Core/Controllers/CodesController.cs).
- URL destination: [routing service](../../../../../ventures/10x-venture-forever-pin/engineering/codebase/forever-pin.backend-services/ForeverPin.Redirect.Api/Infrastructure/Routing/RoutingService.cs).
- Analytics atomicity: [scan flusher](../../../../../ventures/10x-venture-forever-pin/engineering/codebase/forever-pin.backend-services/ForeverPin.Redirect.Api/Infrastructure/Analytics/BackgroundServices/ScanFlushBackgroundService.cs).
- Provider/billing boundary: [Stripe broker](../../../../../ventures/10x-venture-forever-pin/engineering/codebase/forever-pin.backend-services/ForeverPin.Infrastructure/Billing/Services/StripeBillingBroker.cs).
- Test isolation: [product fixture](../../../../../ventures/10x-venture-forever-pin/engineering/codebase/forever-pin.backend-services/ForeverPin.Tests.E2E/Harness/AppFixture.cs).

### Coordinated upgrade gates

1. Completed: autonomous SDK corrections and regression tests; CI correction already published.
2. Completed: full SDK Release solution tested; seven package/symbol pairs verified from one revision.
3. Completed: `10.0.58-beta` published; seven packages available and the tag peels to `546466e`.
4. Implemented locally: ForeverPin host, payload, validation, identity, data and convention adoption.
5. Publish the adoption-discovered SDK corrections; pin the resulting family and run the backend verifier.
6. Complete frontend integration and browser/manual acceptance in the product's separate frontend lane.

### Coverage ledger

| Project | Authored C# files | Audit boundary |
|---|---:|---|
| `ForeverPin.Api` | 22 | Host, controllers, requests, response/error mapping |
| `ForeverPin.Application` | 65 | CQRS contracts, validators, service ports, settings |
| `ForeverPin.Common.Domain` | 6 | Generic serialization duplicates and shared enum ownership |
| `ForeverPin.Common.Persistence` | 0 | Package references and an unused embedded-resource glob; actual SQL is in Persistence |
| `ForeverPin.Domain` | 35 | Entities, content/rule union, payload encoding and enums |
| `ForeverPin.Infrastructure` | 20 | Handlers, repositories, rendering, identity and Stripe |
| `ForeverPin.Persistence` | 6 | Context, configurations, JSON tracking and database types |
| `ForeverPin.Redirect.Api` | 20 | Host, routing, request metadata, queue and flush worker |
| `ForeverPin.Tests.Unit` | 15 | SDK engine vs product logic ownership and known contracts |
| `ForeverPin.Tests.Integration` | 6 | Repositories, provider choice and audit wiring |
| `ForeverPin.Tests.Migrations` | 8 | Runner results and migration guarantees |
| `ForeverPin.Tests.E2E` | 12 | Multi-host configuration, guest/auth/billing and redirect flows |

Build outputs and generated `obj` files are excluded. Frontend implementation, deployment operations and unrelated
SDK vectors are outside this backend adoption sweep. Existing documentation-only completion is not reset by this inventory.
