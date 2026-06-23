# Errors, Results & Exceptions — architecture

*Last updated: 2026-06-22 · Status: **IMPLEMENTED** (mono-lib builds green · 172 tests pass) — the code is the source of truth; this doc is the design record + decisions.*

> SDK error / validation / exception layer. Builds on `docs/analysis/validation-and-result-pattern.md`; pairs with `docs/planning/mediator-cqrs/mediator-cqrs-result-absorption.md`. Conventions still to rewrite to this model: `foundation/result-pattern.md`, `foundation/validation.md`, `presentation/problem-details.md`.
>
> **Investigation scope (how this was derived):** scanned the SDK + apps (drydock, secrets-vault, smart-qr, haven, your-pocket-doctor, prism) + UI lib + conventions; external = MS docs, ErrorOr / FluentResults / Ardalis, RFC 9457 (§8). Web claim-verification was rate-limited → validated by hand vs primary sources.

---

## 0. Implementation status (2026-06-23)

- **Built green** (`WoW.Two.Sdk.Backend.Beta.slnx`, 0 errors) · **198 tests pass** — Foundation.Tests 85 · Mediator.Tests 68 · Web.Tests 31 · Migrations.Tests 14 (regression). SDK only; **products not migrated** (separate coordinated effort).
- **Post-impl fixes:** `DataIntegrity`→`Defect` nature · auth→401/403 via `AppException` (`AuthorizationException` deleted) · `Testing.Integrations/**` added to the shipping mono-lib's `DefaultItemExcludes` (test doubles `FakeContainerRegistryClient`/`FakeGitHubClient` were globbing into the package) · class-level `<paramref>`→`<c>` doc fix (CS1734).
- **Pipeline verified (2026-06-23):** `AddApiDefaults` → `AddAppExceptionHandling` wires every error seam (`IErrorHttpStatusCodeMapper` + `IErrorMessageResolver` via `AddErrorHttpStatusMapping`, `AppErrorObserver`, `IProblemDetailsService` via `AddTraceAwareProblemDetails`); `ExceptionToResultBehavior` is outermost iff registered first (mediator folds registration-order → head). End-to-end throw → `application/problem+json` now guarded by `Web.Tests/ExceptionHandling/AppExceptionHandlingEndToEndTests` over an in-memory TestServer: `AppException`→404 (+`code`/`requestId`) · `ValidationException`→400 (+`errors[]`) · `retryAfter` metadata→`Retry-After` header · unhandled→safe 500 (no detail leak).
- **Exception-mapper seam (2026-06-23):** the blanket `catch → Unexpected` in `ExceptionToResultBehavior` **and** `UnhandledExceptionHandler` is replaced by an injected **`IExceptionMapper`** (`Map(Exception)→AppError`, total). Default `ExceptionMapper` unwraps `AppException` → walks registered **`IExceptionMappingRule`** contributors **last-registered-first** (an app rule shadows an SDK rule) → falls back to `Unexpected`. SDK ships `DbExceptionMappingRule` (the old `DbErrors` switch, `_→null`), auto-wired by `AddPostgresPersistence`. Apps extend via `AddExceptionMappingRule<TRule>()` / `(instance)` for exceptions the SDK does not know, or replace the facade wholesale. Net effect: a Postgres `23505` (or any rule-matched exception) escaping a handler now renders its real status (409) instead of 500. **Supersedes the §3.8 "no generic exception→type" stance.**
- **Deferred:** `HttpClientError` (http-layer) · frontend (§3.12) · product migration · i18n fill · result combinators · convention-doc rewrite.

**Component → file map** (replaces the old inline snippets):

| Component | File(s) under `src/` |
|---|---|
| `AppErrorType` (18 kinds — incl. `BusinessRule`/422, `PaymentRequired`/402, `Gone`/410, `Canceled`/499, `DataIntegrity`) · `ErrorOrigin` · `AppError` (`Of`/`FromException`/`ToException`/`Throw`/`Is`) · `AppException` · `ExceptionChain` · `AppErrors` (catalog) · `ErrorMessages` · `AppAggregateError` | `Foundation/Errors/` |
| `ErrorNature` + `IErrorNatureClassifier` + `DefaultErrorNatureClassifier` · `IErrorMessageResolver` (+default) | `Foundation/Errors/` |
| `FieldError` · `ValidationError : AppError` · `ValidationException` · `IValidator` · `FluentValidationAdapter` | `Foundation/Validation/` |
| `Result` / `Result<T>` · `ResultExtensions` (`ValueOrThrow`/`ThrowIfFailure`) · `AttemptExtensions` (`Attempt`/`AttemptAsync`) | `Foundation/Results/` |
| `AppResult<TSuccess>` (+`Ok`/`Fail`/`Match`) · `AppResultFactory` · `IAppSuccessContext`/`IAppFailureContext` | `Mediator/Result/` |
| `ExceptionToResultBehavior` (+`AddMediatorExceptionToResultBehavior`) | `Mediator/ExceptionHandling/` |
| `AuthorizationBehavior` (throws `AppException` 401/403) | `Mediator/Authorization/` |
| `IErrorHttpStatusCodeMapper` + `DefaultErrorHttpStatusCodeMapper` (incl. `Canceled`=499, `DataIntegrity`=500) | `Web/ErrorMapping/` |
| `AppErrorProblemDetailsFactory` · `AppExceptionHandler` · `UnhandledExceptionHandler` (maps via `IExceptionMapper`) · `ValidationExceptionHandler`/`Filter` | `Web/ExceptionHandling/` |
| `IExceptionMapper` (facade, total) + `IExceptionMappingRule` (contributor, nullable, LIFO) + `ExceptionMapper` (+`AddExceptionMapping`/`AddExceptionMappingRule`) | `Foundation/Errors/ExceptionMapping.cs` |
| `DbExceptionMappingRule` (SDK rule; Npgsql/EF → `AppError`; +`AddDbExceptionMapping`, auto-wired by `AddPostgresPersistence`) · `DbErrors.From` (static convenience, shares the switch) | `Data/Errors/` |
| `AppErrorObserver` (+`AddAppErrorObserver`) | `Observability/Errors/` |
| wiring | `Meta/ApiDefaultsExtensions.cs` (`AddAppExceptionHandling`) |
| **deleted** | `Foundation/Errors/DomainError.cs` · `Foundation/Validation/ValidationResult.cs` · `Mediator/Result/{FailureCategory,ICategorizedFailure,ISuccessResult,IFailureResult}.cs` · `Web/Results/FailureCategoryExtensions.cs` |

---

## 1. Problem solved (was: two competing failure models)

Before: orphan `Result`/`Result<T>`+`DomainError` (HTTP int baked in) **vs** mandated `AppResult<TSuccess,TFailure>`+`FailureCategory`/`ICategorizedFailure`; `result-pattern.md` and `validation.md` described different models; `ValidationError.Code` dropped on the wire; only `ValidationException` mapped; `AuthorizationException`→500; `FailureCategory` duplicated per product. All resolved by the unified model below.

---

## 2. External landscape (informing the design)

- **Throw vs return** — MS: exceptions default + `Try-Parse`/`Tester-Doer`; community: Result for expected, throw for exceptional → make both first-class **and convertible**.
- **Result libs** — **ErrorOr** is the shape template (`Code`+`Description`+`Type`); Ardalis confirms enum→HTTP; we ship our own (no commercial dep; source-gen-first).
- **ProblemDetails** — RFC 9457, `application/problem+json`, extensible (`code`, `errors[]`); ASP.NET `AddProblemDetails`+`IProblemDetailsService`+`IExceptionHandler` (already used) — gap was coverage + emitting `code`.
- **Validation/i18n** — FluentValidation behind `IValidator<T>`; `IStringLocalizer` key-as-fallback enables message-key-as-default.

---

## 3. Design — by layer

> Code is in the files (§0 map). Each layer = the decision; rationale is in §6.

### 3.1 Models — `AppError`, `AppErrorType`, `AppException`
- One open `record AppError(AppErrorType Type, string Message, Metadata?) { ErrorOrigin? Origin }` — **pure value**, no live `Exception`; `Type` name is the wire `code`; `Origin` (caller-info or `ExceptionChain`/stack) is **log-only**. Catalog `AppErrors`; per-type safe default `ErrorMessages.For`. `AppErrorType` is SDK-owned, serialized by name (incl. `Canceled`→499, `DataIntegrity`→Defect/500).
- `AppException(AppError[, inner])` **carries** the error (one source of truth); authored via `AppError`; subclass only for catch-read members. `Guard`/`ArgumentException` stay separate. Validation: `ValidationError : AppError { FieldError[] Failures }` + `ValidationException`; multiplicity → `AppAggregateError : AppError`.

### 3.2 `Result` / `Result<T>` — everywhere carrier
Lightweight closed DU carrying `AppError`; `where T : notnull`; `Match`/`Map`; no context. Non-null + side-ownership from the nested-case DU.

### 3.3 `AppResult<TSuccess>` — mediator ↔ controllers
Collapsed from `<TSuccess,TFailure>` (failure = `AppError`); kept context (`IAppSuccessContext`/`IAppFailureContext`); dropped `ISuccessResult`/`IFailureResult`; `where TSuccess : notnull`; `.Match`; `AppResultFactory.TryCreateFailure` (cached reflection) for the behavior.

### 3.4 Bridge — return ⇄ throw
`error.Throw()`/`ToException()` · `result.ValueOrThrow()`/`ThrowIfFailure()` · `(Func).Attempt()`/`AttemptAsync()` (catch-all → `Result`; **`OperationCanceledException`→`Canceled`**). Retry stays Polly; combinators LATER.

### 3.5 Validation
FluentValidation behind `IValidator<T>`; `Validate→ValidationError?`, `ValidateAndThrow→ValidationException`; `ValidationResult` DU retired.

### 3.6 Mapping — `IErrorHttpStatusCodeMapper`
`int ToStatusCode(AppError)` keyed on `Type` (incl. `Canceled`=499); SDK default + DI override; in `Web` (Foundation stays transport-free). Returns `int` (cast-free with ASP.NET; admits non-standard codes).

### 3.7 ProblemDetails + handlers
Shared `AppErrorProblemDetailsFactory` (controller `.Match` **and** handlers): status + `code` + `type` URN + `detail` + `errors[]` (for `ValidationError`/`AppAggregateError`) + reserved-`Metadata`→headers (`retryAfter`→`Retry-After`); **never emits `Origin`**. Handlers: `AppExceptionHandler` · `UnhandledExceptionHandler` (500, no leak) · `ValidationExceptionHandler/Filter` (outside-mediator fallback). Framework errors (404/405/binding-400) get `code` backfilled from status via `CustomizeProblemDetails`.

### 3.8 Infra classification — `ErrorNature` + exception mapping
Descriptive **`ErrorNature {Transient, Permanent, Defect}`** via `IErrorNatureClassifier` (DI) — consumers derive retry/fallback/log. **Exception→`AppError` is an extensible DI seam** (revised 2026-06-23, see §0): `IExceptionMapper` (total facade) walks ordered `IExceptionMappingRule` contributors (last-registered wins) after unwrapping `AppException`, else `Unexpected`. SDK ships `DbExceptionMappingRule` (Npgsql/EF, auto-wired by `AddPostgresPersistence`); apps add rules for their own exceptions via `AddExceptionMappingRule`. HTTP gets a typed `HttpClientError` (2-layer) / rule **later** (http-layer). `DbErrors.From(ex)` remains as the static, DI-free catch-site convenience over the same switch. (Replaces the original "no generic exception→type" decision.)

### 3.9 Mediator integration — never throw
Terminal `ExceptionToResultBehavior` converts throws → `Failure` (OCE→`Canceled`/`OperationTimeout` handled directly; every other throw → `IExceptionMapper.Map` so `AppException`/DB/app-rule exceptions get their real type, else `Unexpected`; non-`AppResult` response → rethrow to the global handler). Records via `AppErrorObserver`.

### 3.10 Logging & observability
`ILogger<T>`+Serilog unchanged. `AppErrorObserver` = `errors_total` Meter + `Activity.SetStatus(Error)`/`AddException` + level by `ErrorNature` (`Defect`→Error, `Transient`→Warning, `Permanent`→Information). Push `code`+`traceId` to `LogContext`.

### 3.11 i18n groundwork (deferred — hook only)
`IErrorMessageResolver` (default passthrough to `Message`); always emit `code`; fine-grained later via `MessageKey`.

### 3.12 Frontend display (deferred)

| Failure | Surface |
|---|---|
| Validation (400 + `errors[]`) | inline per-field (`setError`) |
| Domain on submit (409/422) | `Alert` / modal status |
| Transient (network/503) | `Toaster` danger |
| NotFound on page load | dedicated empty/404 state |
| Unexpected (500/render) | `ErrorBoundary` → error page (NEW) |
| Auth (401) | redirect to login |

Extend `ApiError` to parse `code`+`errors[]`; per-field map; toast-for-errors; `ErrorBoundary`. No i18n now.

### 3.13 Cross-cutting status
Cancellation ✅ · infra-classification ✅ · logging/observability ✅ · headers ✅ · serialization (`[JsonIgnore] Origin`) ✅ · mediator never-throw ✅ · framework errors ✅ · multi-error (`AppAggregateError`) ✅ · equality (skipped; `error.Is(AppErrorType)`) ✅. Deferred: result combinators · success-context spec · HTTP client model.

---

## 4. Naming (final)

| was | now |
|---|---|
| `DomainError` / `DomainErrorCategory` / `.StatusCode` | `AppError` / `AppErrorType` / *(removed → `IErrorHttpStatusCodeMapper`)* |
| per-product `FailureCategory` / `I{App}Failure` / `ICategorizedFailure` | `AppError` + shared `AppErrorType` |
| thrown wrapper / `AuthorizationException` | `AppException` |
| `AppResult<TSuccess,TFailure>` | `AppResult<TSuccess>` (failure = `AppError`) |
| `ValidationError(Property,Message,Code)` | `FieldError`; aggregate `ValidationError : AppError` |
| `Backbone…GetValue/GetValueAsync` | `Attempt` / `AttemptAsync` |
| `AppError.Description` | `AppError.Message` |
| `ISuccessResult`/`IFailureResult` | *removed* |
| `IApplicationSuccessContext`/`…Failure` | `IAppSuccessContext` / `IAppFailureContext` |
| `ErrorDisposition` + `ErrorPolicy` | `ErrorNature` + `IErrorNatureClassifier` |
| `IExceptionClassifier` | `IExceptionMapper` (facade) + `IExceptionMappingRule` (DI contributors; SDK `DbExceptionMappingRule`, app-extensible); catch-all `Unexpected` |
| multi-error / cancel kind | `AppAggregateError` · `AppErrorType.Canceled` (499) · `DataIntegrity` |

---

## 5. Status by component

**DONE (shipped, tested):** `AppError`/`AppErrorType`/`AppException`/catalog · `Result`/`Result<T>` · `AppResult<TSuccess>` · bridge · validation reshape · `IErrorHttpStatusCodeMapper` · PD factory + handlers (+`code`/`errors[]`) · `ErrorNature`+classifier · **`IExceptionMapper`/`IExceptionMappingRule` seam + `DbExceptionMappingRule`** · `ExceptionToResultBehavior` · `AppErrorObserver` · auth→403 · deletes · **`AddApiDefaults` wiring + end-to-end exception→ProblemDetails pipeline (Web.Tests E2E)**.
**NEXT:** product migration (drydock/smart-qr → `AppResult<TSuccess>`+`AppError`) · app error catalogs (`OrderErrors.*`) + drydock reference adoption.
**LATER:** `HttpClientError` (http-layer) · result combinators (`Map`/`Bind`/`Tap`/`Ensure`) · i18n fill (`IStringLocalizer`+`MessageKey`, FE label-map) · frontend (`ApiError`/per-field/`ErrorBoundary`) · error-code analyzer · `FieldError`→`ValidationFailure` rename.

---

## 6. Decisions (resolved)

- **Error model:** one `AppErrorType` enum + `AppError` value; no generic `AppError<TCode>` (viral); `AppException` *carries* `AppError` (one source of truth) — rejected symmetric peers.
- **Origin/exception:** `AppError` is a pure value; caught exceptions fold in via `FromException` (stack→`Origin`, type→`causeChain` log-only) + native `InnerException` on throw.
- **Mapping:** `int` not `HttpStatusCode` (cast-free; non-standard codes). `Canceled`=499 validated the choice.
- **Cancellation:** OCE **converted** (not rethrown) — `Canceled`(499)/`OperationTimeout`(504); `finally` cleanup intact; mediator never throws.
- **Classification:** identity (`AppErrorType`) ≠ nature (`ErrorNature`, descriptive); consumers decide retry/fallback.
- **Carriers:** two — `AppResult<TSuccess>` (mediator, context) + `Result<T>` (everywhere); both carry `AppError`.
- **Equality:** skipped (record `==` footgun) — use `error.Is(AppErrorType)`.

**`AppErrorType` set — resolved (18):** added `BusinessRule`/422, `PaymentRequired`/402, `Gone`/410 from the drydock ∪ smart-qr ∪ secrets-vault ∪ pocket-doctor scan; folded `DecryptionFailed`→`DataIntegrity`, `DbConcurrency`→`Conflict`, sealed-503→`ExternalUnavailable`.
**Open (small):** `D-type-uri` (URN now) · `D-conventions-distill` (rewrite the 3 docs).

---

## 7. Parked ideas (later)

- **`HelpLink`** on `AppError` → docs page for complex errors (maps to ProblemDetails `type`).
- **Remediation / guidance** ("fix a,b,c" / "retry") beside `Message`.
- **User report / flag-for-priority** → ops dashboard priority bump (likely `drydock`).

---

## 8. Sources

MS: design-guidelines/exceptions · exceptions/best-practices · aspnet/core error-handling-api · aspnet/core localization. Repos/specs: github.com/error-or/error-or · github.com/altmann/FluentResults · rfc-editor.org/rfc/rfc9457. Blogs: milanjovanovic.tech · andrewlock.net · swagger.io. Ardalis.Result / CSharpFunctionalExtensions / language-ext from knowledge. *(Harness verify was rate-limited; validated by hand.)*
