# ForeverPin extractions

*Last updated: 2026-09-28*

> SDK vectors triggered by ForeverPin's local infrastructure after the `10.0.59-beta` adoption.
> Each iteration builds the reusable mechanism in the SDK; product adoption follows the published package.

## Rulings — 2026-09-27

- All nine candidates are confirmed. Per-key transaction locks were confirmed explicitly.
- Redirect target checks never reject a value: findings return as warnings or suggestions.
  Some legitimate targets resolve only behind a VPN, login or intranet.
- Validation infrastructure must carry warnings through a successful write.
- IP geolocation uses free data now; paid databases and their licences come later.

## Status

- [x] E1 — per-key transaction locks: PostgreSQL contention, ordering and cancellation tests pass
- [x] E2 — warning-state validation and redirect target checks: interceptor, tracker, envelope and 18 target cases
- [x] E3 — free IP geolocation: MMDB broker, hot reload and monthly DB-IP Lite refresh; 16 tests
- [x] E4 — deployment hosting: trusted proxies, probe hosts, persistent keys, database readiness; 12 tests
- [x] E5 — queued batch background work: bounded pipeline, scoped handler, shutdown drain; 5 host tests
- [x] E6 — unique key allocation under a savepoint: SQLite and PostgreSQL conflict tests
- [x] E7 — commit-aware cache invalidation across hosts: PostgreSQL commit, rollback and reconnect tests
- [x] E8 — inbound webhook deduplication and ordering: inbox redelivery and watermark tests on PostgreSQL
- [x] E9 — SPA antiforgery: token issue, rejection, echo and exemption tests

---

## E1 — Per-key transaction locks

Source: `DbContextOwnerLockExtensions`, which serializes plan-cap, guest-claim and subscription writes.

- `DatabaseFacade.AcquireTransactionLocksAsync(keys)` hashes each key, sorts and locks it for the transaction.
- PostgreSQL uses `pg_advisory_xact_lock`; SQL Server uses `sp_getapplock`; SQLite serializes writers already.
- An absent transaction or an unsupported provider fails loudly.
- Acceptance: a second transaction on the same key waits for the first commit; distinct keys do not block.

## E2 — Warning-state validation and redirect target checks

Source: `UrlContentValidator` and routing's inline target check.

- The mediator validation interceptor inspects once: errors still throw; warnings and suggestions are tracked.
- A scoped tracker holds the request's advisories; a web mapper returns them with localized messages.
- `RedirectTargetValidator` reports private-network, insecure, credential, IP-literal, port and IDN findings.
  It never blocks: structural URL rules remain the product's errors.
- Acceptance: a warning-only request reaches its handler, and the response can carry each advisory.

## E3 — Free IP geolocation

Source: `NoopGeoBroker`; country routing cannot match anything today.

- `IIpLocationBroker` fronts an MMDB reader, so free and paid databases share one contract.
- The default free source is DB-IP IP-to-Country Lite (CC BY 4.0), downloaded monthly when enabled.
  Products must show the attribution the licence requires.
- Private, reserved and unknown addresses return no location.
- Acceptance: a generated test database resolves country data; a failed refresh keeps the previous database.

## E4 — Deployment hosting

Source: both hosts' `DeploymentHosting` and `DatabaseReadinessCheck` copies.

- Forwarded headers trust loopback plus configured proxies and networks only; the SDK default trusted every sender.
- Probe hosts are appended to a restricted host allowlist, so loopback health checks pass.
- Persistent Data Protection keys are required outside Development.
- A database readiness check resolves the context per probe.

## E5 — Queued batch background work

Source: `ChannelScanRecorder` and `ScanFlushBackgroundService`.

- A bounded queue counts dropped items instead of blocking producers.
- A flush service reads batches, runs a scoped handler per batch and drains on shutdown.
- Metrics cover enqueued, dropped, processed and failed items.

## E6 — Unique key allocation

Source: `CodeRepository.AddWithinLimitAsync` and the slug retry loop.

- An insert under a savepoint reports a unique conflict without poisoning the outer transaction.
- A bounded allocator retries generated keys and returns a conflict when attempts run out.

## E7 — Commit-aware cache invalidation

Source: the unwired `CachedRedirectCodeRepository`.

- PostgreSQL `NOTIFY` delivers invalidations only when the writing transaction commits.
- Each host evicts its local entries; a lost connection clears the local cache.
- A time-to-live bounds staleness when a notification is missed.

## E8 — Inbound webhook deduplication and ordering

Source: Stripe webhook handling and gap finding F08.

- The EF inbox deduplicates provider event IDs in the effect's transaction.
- A monotonic event-time guard skips events older than the applied state.
- Entitlement policy stays in the product.

## E9 — SPA antiforgery

Source: cookie-authenticated mutations without a CSRF token.

- A readable token cookie and a request header protect unsafe methods that carry cookies.
- Explicit exemptions cover signed provider callbacks such as webhooks.
- Frontend adoption needs the frontend lane to send the header.

---

## ForeverPin adoption — adopted, awaiting publication

One product cut consumed all nine iterations against local candidate `10.0.60-beta.local.20260927.3`.
ForeverPin's commits pin `10.0.60-beta`: push this repository first, then re-run ForeverPin's backend verifier.

- [x] E1–E9 adopted in ForeverPin commits `f8ad3eb`–`b9b2397`; 238 backend and 7 frontend tests pass
- [ ] Publish `10.0.60-beta` and verify ForeverPin against it without the candidate feed

Adoption follow-ups added to the SDK in the same release:

- `validator.Validate(instance, advisories)` records advisories from handler-side validation, such as a loaded mode.
- `AddMemoryCacheInvalidationHandler()` evicts `IMemoryCache` entries; ForeverPin caches live code objects.
- `AddPostgresCacheInvalidation()` defaults its connection to the `AddPostgresPersistence` database.
- `DbIpLiteOptions.EnableDownload = false` serves a provisioned file, so test hosts never download.
- Testing: `SpaAntiforgeryHandler` and `CreateSpaClient(tokenPath)` echo tokens as the browser client does.

Deviations from the plan below, both deliberate:

- E6: the repository keeps one insert attempt per call through `TrySaveChangesUniqueAsync`; the handler's bounded
  retry stays beside its plan checks, so `SaveWithGeneratedKeyAsync` is unused.
- E8: subscription writes already hold the owner lock, so a tracked watermark check keeps audit stamps;
  `ExecuteUpdateIfNewerAsync` would bypass the audit interceptor. Same-second events apply in arrival order.


| Iteration | Product change |
|---|---|
| E1 | `AcquireOwnerLocksAsync` → `Database.AcquireTransactionLocksAsync("owner", ids)`; the key hash changes, so every writer upgrades together. |
| E2 | `RuleFor(c => c.Url).AdviseOnRedirectTarget()` in its own rule; create/update return `ApiResponse<T>.Ok(dto, advisories.Map(HttpContext))`; the frontend renders `warnings`. |
| E3 | Redirect host: `AddDbIpLiteIpLocation` on a writable volume; `IGeoBroker` delegates to `IIpLocationBroker`; the frontend shows the DB-IP attribution. |
| E4 | Both `DeploymentHosting` copies → `AddPersistentDataProtection`, `TrustedProxies`/`TrustedNetworks`, `AddDatabaseReadinessCheck<AppDbContext>()`. |
| E5 | `ChannelScanRecorder` + `ScanFlushBackgroundService` → `AddBatchPipeline<ScanRecordModel, ScanBatchHandler>()`. |
| E6 | Slug insert savepoint → `SaveWithGeneratedKeyAsync(..., "ix_codes_slug")`. |
| E7 | Redirect host wires the cached repository with `AddPostgresCacheInvalidation`; code writes publish `code:{slug}` inside their transaction. |
| E8 | `AddEfInbox<AppDbContext>()` + `inbox_messages` and `last_event_at` migrations; the Stripe broker surfaces event id and time. |
| E9 | `AddSpaAntiforgery` exempting the webhook; the frontend sends `X-XSRF-TOKEN` in the same release, or cookie writes fail. |
