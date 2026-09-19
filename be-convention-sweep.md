# Backend convention sweep

*Last updated: 2026-09-19*

## Status

✅ **SDK commit batches and the follow-up convention sweep are complete.** `10.0.57-beta` contains the CI correction.
The autonomous SDK adoption cut is implemented; its release verification is recorded in the adoption track.
The [ForeverPin adoption sweep](engineering/planning/foreverpin-adoption/foreverpin-adoption.md) owns the newly
identified behavioral fixes and the combined upgrade cut; earlier convention completion does not close those findings.
Fresh verification and the signed commit inventory are in the
[batch verification report](../../../system/sessions/backend-beta-build/commit-batches-verification.md).
This is the active handoff. Completed/refuted rows, old measurements and superseded discussion
are retained in [the historical snapshot](be-convention-sweep-history.md).
Historical completion markers are not a fresh source or runtime certification.
[Live SDK recheck](../../../system/sessions/backend-beta-build/sdk-handoff-recheck.md) confirms the
completed session naming/placement slices. The CloudEvents decoder's reopened N91 failure-contract
work is also complete; its fresh verification is linked below.

## Execution

- Finish SDK implementation and scoped checks; CI/release work follows the implementation sweep.
- Rule owners live under `wow-two-ws/conventions/development/backend/dotnet/`; paths below are relative to it.
- Recheck each row against current source and its owner. Historical counts are inventory leads.
- Preserve row IDs. Highest naming ID is N114; only a distinct obligation earns N115.
  Historical N94–N98 duplicates require subject-qualified references; new work extends an existing row.
- Product adoption is separate: the 31 product rows live in
  `workbench/ventures/10x-venture-forever-pin/foreverpin-be-update.md`.
- Use `engineering/planning/sweep.sh` for convention checks; source checks do not replace runtime checks.
- Ordinary agent commits require the workspace's explicit turn-scoped commit switch; the developer publishes.
  Breaking SDK changes are approved; no production consumers exist.

### Workstream order

1. Completed: drain the SDK's pending changes in cohesive, reviewed commit batches.
2. Completed: sweep the SDK against the conventions again and resolve mechanical findings.
3. Completed: CI publication-verifier correction committed as `a645d1b`, released in `10.0.57-beta`.
4. Execute the full ForeverPin SDK-facing sweep: correct autonomous SDK gaps and preserve deferred policy decisions.
5. Publish the completed candidate and upgrade ForeverPin once, including its product adoption changes.
6. Resume missing SDK vectors from the recorded deferred decisions, rather than opening them during this cut.

Other consumer repins remain recorded below; they do not precede the requested ForeverPin migration.

The sweep includes data-safety slices such as tracked writes and migration guarantees. The full data-session build is
larger than this sweep and remains owned by `engineering/planning/data-pipeline/`. Localization request culture and
formatters exist; complete translation/i18n remains a later vector in `targets.md` and the errors architecture.

## Remaining SDK work

### Release-dependent consumer adoption

- [ ] When TranscriptForge is repinned from `WoW2.Sdk.Backend.Beta` `10.0.44-beta` to the published sweep version,
  add the new caption parser namespace in its fetcher and VTT tests, and run its consumer checks.
  Exact files/imports: [parser verification](../../../system/sessions/backend-beta-build/parser-conformance-verification.md#reference-inventory-and-adoption).
  This adoption task is separate from the 27 SDK implementation rows; it depends on the published version.
- [ ] Repin the other direct consumers to the published sweep version and repair only the APIs each uses:
  ForeverPin (`10.0.45-beta`), TransportBrain (`10.0.45-beta`), Tnis (`10.0.45-beta`), Drydock (`10.0.40-beta`),
  SecretsVault (`10.0.40-beta`), Sift (`10.0.21-beta`), Arcade (`10.0.21-beta`), MuseumsGallery (`10.0.21-beta`) and
  TnisMintrans (`10.0.21-beta`). Repin the product template (`10.0.21-beta`) separately so new ventures start current.

## Scope retained from confirmed decisions

- C25 retains the phase/job ordering check from N98 (interceptor vocabulary), including
  `ClaimCheckRehydratingConsumeInterceptor`; a completed rename does not establish pipeline behavior.
- N100/N101 vocabulary is settled: Parser, Exporter, Formatter, Transport, Serializer and Bus are approved.
  Integrity checks use Validator; Google token evidence uses Authenticator. Internal wrappers use Model;
  actual event payloads retain Event. No naming decision remains pending.

## Verification evidence

- [2026-09-19 batch verification](../../../system/sessions/backend-beta-build/commit-batches-verification.md):
  Release checks pass 455 tests with one intentional Kafka skip. The follow-up fixes cover inbox lock lifecycle,
  annotated release tags, four remaining helper roles and the startup child probe's build configuration.
- [Naming inventory and report index](../../../system/sessions/backend-beta-build/sdk-naming-inventory.md).
- [SDK handoff recheck](../../../system/sessions/backend-beta-build/sdk-handoff-recheck.md).
- [Retained-role verification](../../../system/sessions/backend-beta-build/retained-role-conformance-verification.md):
  Exporter/Serializer/Bus placement, scoped compilation and 26 existing serializer/pump tests.
  These do not establish exporter runtime contracts, GeoJSON fidelity,
  provider delivery guarantees or whole-SDK release readiness.
- [CloudEvents decoder verification](../../../system/sessions/backend-beta-build/cloudevents-decoder-verification.md):
  complete-document parsing and failure results for malformed JSON/base64; all 37 serializer tests passed.
  Inbound context attributes remain outside this payload decoder's semantic validation contract.
- [N101 verification](../../../system/sessions/backend-beta-build/n101-conformance-verification.md):
  Transport placement, saga classification, GeoJSON contracts and Google cancellation are complete.
- [N111 verification](../../../system/sessions/backend-beta-build/n111-options-registration-verification.md):
  SDK-owned code options use direct values or startup-validated pipelines; only topology composers retain raw
  `AddOptions<T>`, and resolving delayed-retry options no longer mutates the service collection.
- [N115 verification](../../../system/sessions/backend-beta-build/n115-dependency-remediation-verification.md):
  all 14 projects are vulnerability-clear against live NuGet data; the solution builds and 399 tests pass.
- [C15 verification](../../../system/sessions/backend-beta-build/c15-api-defaults-pipeline-verification.md):
  the host-controlled identity seam runs after routing and before limiter/cache policies; all 47 Web tests pass.
- [C16 verification](../../../system/sessions/backend-beta-build/c16-build-packaging-verification.md):
  test projects evaluate non-packable, seven release projects remain packable and local/CI use the same SDK pin.
- [C17 verification](../../../system/sessions/backend-beta-build/c17-release-pipeline-verification.md):
  CI tests one release commit, verifies all seven package pairs and records the exact revision/version manifest.
- [C18 verification](../../../system/sessions/backend-beta-build/c18-test-host-isolation-verification.md):
  both clock APIs share one test clock; host configuration and database-provider selection are instance-local.
- [C19 verification](../../../system/sessions/backend-beta-build/c19-migration-guarantees-verification.md):
  provider coordination, recovery, enum boundaries, SQLite rebuilds and CLI exits pass 26 migration tests.
- [C20 verification](../../../system/sessions/backend-beta-build/c20-jwt-trust-verification.md):
  one key source, secure metadata and algorithm-specific key requirements pass all 20 Identity tests.
- [C22 verification](../../../system/sessions/backend-beta-build/c22-observability-contract-verification.md):
  SDK telemetry collection, correlation, bounded tags and exporter isolation pass 69 Mediator and 121 Messaging tests.
- [C23 verification](../../../system/sessions/backend-beta-build/c23-startup-failure-reporting-verification.md):
  pre-host and options-validation child failures persist durably and exit nonzero; all 52 Web tests pass.
- [C24 verification](../../../system/sessions/backend-beta-build/c24-http-replay-safety-verification.md):
  unsafe methods run once by default, explicit idempotency selectors permit replay, and nine focused tests cover
  cancellation, total budgets, streaming rejection and disposal.
- [C25 verification](../../../system/sessions/backend-beta-build/c25-tenant-messaging-guarantees-verification.md):
  EF and generated Dapper CRUD enforce ambient tenant scope; messaging documents and tests at-least-once delivery,
  atomic durable dedupe/effect, bounded queues/retries, failed-outbox retention and forced-final claim-check rehydration.
- [Release readiness](../../../system/sessions/backend-beta-build/release-readiness-verification.md):
  the Release solution builds, 453 tests pass with one intentional Kafka skip, and all seven package/symbol pairs
  pass the metadata, asset, dependency and revision verifier at `10.0.55-beta`.
