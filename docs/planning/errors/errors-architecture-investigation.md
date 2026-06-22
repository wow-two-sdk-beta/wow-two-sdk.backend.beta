# Errors, Results & Exceptions — architecture investigation

*Last updated: 2026-06-21 · Status: **investigation / no code** — under review (see Review log)*

> Redesign of the SDK error / validation / exception layer. Builds on `docs/analysis/validation-and-result-pattern.md`; pairs with `docs/planning/mediator-cqrs/mediator-cqrs-result-absorption.md`. Conventions touched: `foundation/result-pattern.md`, `foundation/validation.md`, `presentation/problem-details.md`.

**Scope:** scanned the SDK, the apps (drydock, secrets-vault, smart-qr, haven, your-pocket-doctor, prism), the UI lib + conventions; external = MS docs, ErrorOr / FluentResults / Ardalis, RFC 9457. Current-state = read from source (file:line). Web verify step was rate-limited, so claims were validated against primary sources by hand (§7).

> **Review log — 2026-06-21** (supersedes §1/§4 body where they differ; consolidated rewrite pending the ⏳ items)
> - ✅ **No generic `AppError<TCode>`** — viral type params + can't compose cross-domain.
> - ✅ **Naming:** `Error`→`AppError`, `ErrorException`→`AppException` (matches `AppResult`). `your-pocket-doctor`'s `AppException` = the anti-pattern to replace.
> - ✅ **Failure category dropped** — no coarse HTTP-category enum on the error; controller maps `code → status` via one mapper.
> - ✅ **Code = SDK-owned enum `AppErrorType`** (no meaningful numeric — **name is the contract**: JSON via `JsonStringEnumConverter` → `"DbTimeout"`; DB via PG enum `HasPostgresEnum` if persisted) — abstract failure *kinds* reused everywhere: `DbTimeout`, `OperationTimeout`, `FileNotFound`, `ExternalUnauthorized`, `ExternalUnavailable`, `SerializationFailed`, `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `TooManyRequests`, `Unexpected`. SDK-owned is fine (you control the SDK); **no app-specific codes** — specificity lives in `Message` + call-site + `Metadata`. The `AppErrorCode` VO is **dropped** (would carry nothing extra: status is external, message is per-call-site). Field = `AppError.Type`. *(The earlier "wire collapses to string" caveat applied only to a `System.Enum`-typed open field — not this single typed enum.)* Shape → `AppError(AppErrorType Type, string Message, IReadOnlyDictionary<string,object?>? Metadata = null) { ErrorOrigin? Origin }` — `Message` (was `Description`) mirrors `Exception.Message`.
> - ✅ **Visibility** — `AppError.Type` (by name) is emitted on the wire (ProblemDetails `code`) and readable by controllers / response formatters / any layer/observer.
> - ✅ **Call-site capture** — the `AppError` factory takes `[CallerMemberName]/[CallerFilePath]/[CallerLineNumber]` → optional `Origin(member, file, line)`. Closes the gap on the **Result (no-throw) path** (no stack trace; the throw path already carries the `AppException` stack). Compile-time literals → ~zero cost. **Log/telemetry only — never on the public wire** (path leak); dev-only if ever surfaced.
> - ⏳ **2 DI mapper seams** (stable interface names, SDK default + app override): `IErrorStatusMap` (`AppErrorType → status`, per transport; small SDK default map + app override) and `IErrorMessageResolver` (`(error, culture) → message`; default = passthrough to `Message`). Translation is this resolver — a separate "description mapper" is redundant.
> - ⚠️ **Translation granularity (deferred, non-blocking)** — abstract enum + per-call-site `Message` ⇒ near-term translation is *kind-level* (keyed on `AppErrorType`); the authored `Message` stays single-language. Fine-grained translated messages later = add an optional `MessageKey`. Resolver seam keeps it open.
> - ✅ **Metadata — locked** (one bag, name `Metadata`): message-template args **and** diagnostic/wire context (`retryAfter`, `dependency` → ProblemDetails extensions). Not `Parameters` (a value like `retryAfter` is both arg + extension → one bag). Consumer-read fields → typed `AppError`/`AppException` subtype, not the bag.
> - ✅ **Two result carriers, one error** — `AppResult<TSuccess>` for mediator handlers (CQRS return + context channels); `Result`/`Result<T>` for everywhere else (domain/foundation/infra, lightweight, **no context channels**). Both carry the **same `AppError`**; inner `Result<T>` maps up to `AppResult`. *(= your "AppResult + XResult".)*
> - 📌 **Parked ideas (later brainstorm — not in scope now):**
>   - **`HelpLink`** on `AppError` (reuse `Exception.HelpLink` on the throw side) → URL to a docs page for complex errors (what/why/what-to-do); maps to ProblemDetails `type` (RFC: `type` SHOULD document the problem) or a `helpLink` extension.
>   - **Remediation / guidance** — optional structured "what to do" (`"fix a, b, c"` / `"retry"`), distinct from `Message` (what happened); surfaced beside the message (forms: field errors + a guidance line).
>   - **User report / flag-for-priority** — a "report this error" action tying an occurrence (`Type` + `traceId` + `Origin`) to an ops dashboard that bumps its priority — telemetry/ops feature, likely lands in `drydock` (ops plane).
> - ✅ **`AppResult<TSuccess>` collapse (core-problem block)** — drop the second type param → `Failure(AppError Error, ctx?)`. **Pattern matching preserved** (still `Success`/`Failure` cases + `.Match`). Deletes per-product `I{App}Failure` + per-op `{Op}Result.Failure`. **Revises `result-pattern.md` + `mediator-cqrs-result-absorption.md`.**
>   - **Non-null / side-ownership guarantee survives** — it comes from the **closed nested-case DU** (private ctor; `Success(TSuccess Data, ctx?)` / `Failure(AppError Error, ctx?)`), *not* from the 2nd type param: `Data` is non-null & only on `Success`, `Error` non-null & only on `Failure`; no shared/nullable field, no `bool IsSuccess; T?`; access gated by `.Match` → zero null-checks. Add `where TSuccess : notnull` (+ NRT) to forbid `AppResult<CodeDto?>`.
>   - **Drop `ISuccessResult` marker** — it constrained *which types* may be success (orthogonal to nullability); asymmetric ceremony once `IFailureResult`/failure-marker is gone. Use `where TSuccess : notnull` for the real guarantee. "Explicit success container" stays a *convention* (`{Op}Result.Success` record), not a constraint. (Context markers `IApplicationSuccessContext`/`IApplicationFailureContext` stay — `AppResult` only.)
> - ✅ **`AppException`** — single non-sealed `AppException(AppError error[, Exception inner]) : Exception` carrying the `AppError` (`Message`=`error.Message`); **authored via `AppError` + `.Throw()`/`.ToException()`** (no duplicate factories — exception = "AppError in flight"); `catch (AppException)` = known/mapped, else 500; `Guard`/`ArgumentException` stay separate (programmer error). `AppErrorType` doubles as **status + retry** classifier (`DbTimeout`/`ExternalUnavailable` = transient → Polly). Reject per-kind hierarchy (rebuilds the enum as types).
>   - **Validation = canonical subtype** — `ValidationError : AppError { IReadOnlyList<FieldError> Failures }` (`Type=Validation`); per-field record `ValidationError(Property,Message,Code)` renamed → **`FieldError`** (backlog: maybe `ValidationFailure`). `ValidationException : AppException` whose `.Error` *is* a `ValidationError` (no duplicate list). → validation can be **returned** (`Result.Fail(validationError)`) **or thrown** (`validationError.Throw()`); handler emits `errors[]` from `Failures`.
>   - **Bridge — adopt the existing `Backbone…Exceptions.Catching` convention** (`Func<T>.GetValue()` / `Func<Task<T>|ValueTask<T>>.GetValueAsync()`, which returned `FuncResult<T>`) but **return our `Result<T>`** (carrying `AppError`): catch-all → `AppException` ⇒ `Fail(ex.Error)`, else ⇒ `Fail(AppError(Unexpected, inner: ex))` (`Origin` captured). Using `GetValue`/`GetValueAsync` **is** opting into catch-all (boundary tool — don't blanket the app). Throw side: `error.Throw()` · `result.ValueOrThrow()` · `result.ThrowIfFailure()`. Async result = `Task<Result<T>>`/`ValueTask<…>` (no `AsyncResult`). Retire `FuncResult<T>` → `Result<T>`. Retry stays on Polly (keyed on `AppErrorType`). Railway `Map`/`Bind`/`Tap`(+`*Async`) = LATER.

---

## 1. Recommended model (the answer)

One `Error` concept, two surfaces, one mapping seam:

- **`Error(Code, Description, ErrorType, Metadata?)`** — universal, transport-agnostic. Replaces `DomainError` **and** per-product `FailureCategory`. SDK ships base `ErrorType` + factories; apps add catalogs of concrete `Error`s.
- **Return it** (`Result`/`Result<T>`/`AppResult`) **or throw it** (`ErrorException(Error)`) — switch at any point via a bridge (`error.Throw()`, `result.ValueOrThrow()`, `result.ThrowIfFailure()`, `Result.Try(...)`).
- **Mapping at the edge only:** `Error.Type` (semantic) is readable at any layer; `ErrorType → HTTP status` runs once at presentation → RFC 9457 ProblemDetails. **No HTTP int on the error** (kills `DomainError.StatusCode`).
- **i18n-ready, not now:** `Code` = stable key, `Description` = default string; always emit `code` on the wire so server (`IStringLocalizer`) or client (label-map) can translate later.

---

## 2. Current state — the core problem

**Two competing failure models:**

| | Foundation (orphan) | Mediator (mandated, real) |
|---|---|---|
| Carrier | `Result`/`Result<T>` | `AppResult<TSuccess,TFailure>` |
| Error | `DomainError(Code,Message,Category,Detail?)` | per-op `Failure` : `I{App}Failure(ErrorMessage,FailureCategory)` |
| Category | `DomainErrorCategory` (9, **HTTP int baked in**) | `FailureCategory` (6, no int) |
| HTTP map | `DomainError.StatusCode` (on the error) | `ApiResults.ToStatusCode` (presentation) |
| Used by | ~nothing | drydock, secrets-vault, smart-qr |

`result-pattern.md` and `validation.md` document *different* models; products never touch `DomainError`. A 3-variant `Error` union was already tried and dropped (branch-on-kind) — but the ask now is a single `Error` + bridge, not a union.

**Gaps / bugs:**
- `ValidationError.Code` **dropped on the wire** (`problem-details.md:26`) → blocks i18n + per-field form UX.
- No general `Error`→ProblemDetails handler; only `ValidationException` is mapped. `AuthorizationException` → **500 not 403**.
- `FailureCategory` duplicated per product (`result-pattern.md:116` flags DRY-lift) with deltas (`Unavailable` 503, `PaymentRequired` 402).
- `your-pocket-doctor` = anti-pattern (pre-SDK): everything throws, status welded on `AppException`.
- FE: `ApiError`+ProblemDetails parse exists; `Alert`(inline)+`Toaster` exist; **no** per-field mapping, error boundary/page, toast-for-errors standard, or i18n.

---

## 3. External landscape

- **Throw vs return** — MS: exceptions are the default, but `Try-Parse`/`Tester-Doer` for routine failures; community: Result for *expected* failures, throw for *exceptional*. → make both first-class + convertible.
- **Result libs:**

| Lib | Error shape | Multi | Code | Note |
|---|---|---|---|---|
| **ErrorOr** | `Error(Code,Description,Type,Metadata)`; 7-type enum + `Custom` | ✅ | ✅ first-class | **target template** |
| FluentResults | `IError`/reasons graph | ✅ | ⚠️ metadata | heavier |
| Ardalis.Result | `Result<T>`+`ResultStatus` enum (≈our `FailureCategory`) | ✅ | ⚠️ | confirms enum→HTTP approach |
| CSharpFunctionalExt / language-ext | generic `E` / `Fin`/`Either` | varies | varies | full-FP, heavier |

  → ship our own ErrorOr-shaped `Error` (no commercial dep; SDK is beta-forever/source-gen-first).
- **ProblemDetails** — RFC 9457 (obsoletes 7807), `application/problem+json`, core `type/title/status/detail/instance`, **extensible** (`code`, `errors[]`). ASP.NET Core: `AddProblemDetails`+`IProblemDetailsService`+`IExceptionHandler` (already used) — gap is coverage + emitting `code`.
- **Validation/i18n** — keep FluentValidation behind `IValidator<T>`; `IStringLocalizer` returns the key as fallback → enables message-key-as-default with no `.resx`.

---

## 4. Design specifics

**Shapes (illustrative):**

```csharp
public sealed record Error(string Code, string Description, ErrorType Type,
    IReadOnlyDictionary<string, object?>? Metadata = null)
{
    public ErrorException ToException() => new(this);
    public void Throw() => throw ToException();
}
public enum ErrorType { Failure, Validation, Unauthorized, Forbidden, NotFound,
    Conflict, BusinessRule, TooManyRequests, Unavailable, Unexpected }

public class ErrorException(Error error) : Exception(error.Description) { public Error Error { get; } = error; }
```

```csharp
public static class OrderErrors { public static Error NotFound(Guid id) =>
    new("orders.not_found", $"Order {id} not found.", ErrorType.NotFound); }

Result<Order> Get(Guid id)     => _repo.Find(id) is {} o ? Result.Ok(o) : Result.Fail(OrderErrors.NotFound(id)); // return
Order GetOrThrow(Guid id)      => _repo.Find(id) ?? throw OrderErrors.NotFound(id).ToException();                // throw
```

**Bridge (return ⇄ throw):** `error.Throw()` · `result.ValueOrThrow()` · `result.ThrowIfFailure()` · `Result.Try(() => …)` (catches `ErrorException`→`Fail`).

**ProblemDetails wire shape:** core fields + `code` (NEW, stable key) + `traceId`/`requestId` (kept) + `errors:[{property,code,message}]` (validation, NEW — currently code is dropped).

**Server pipeline:** keep `AddTraceAwareProblemDetails`; extend `ValidationExceptionHandler` to emit `code`+`errors[]`; **add** general `ErrorExceptionHandler` + fallback-500 (no leak); fix auth→403.

**Frontend display matrix:**

| Failure | Surface |
|---|---|
| Validation (400+`errors[]`) | inline per-field (`setError`) |
| Domain on submit (409/422) | `Alert` at form top / modal status |
| Transient (network/503) | `Toaster` danger |
| NotFound on page load | dedicated empty/404 state |
| Unexpected (500/render) | `ErrorBoundary` → error page (NEW) |
| Auth (401) | redirect to login (interceptor) |

**i18n hooks:** `Code` never localized; always emit `code`; later add server `IErrorDescriber`/localizer keyed by code + FE `errorCode→message` map (mirrors enum label-`Record`).

**Reconciliation:** unify the *error* (one `Error`), keep the *carrier* — re-point `AppResult.TFailure` to `Error`, replace per-product `FailureCategory` with SDK `ErrorType`+one map; keep `Result<T>` as the lightweight non-mediator primitive carrying the same `Error`.

---

## 5. Naming — kill "DomainError"

`DomainError`→**`Error`** · `DomainErrorCategory`→**`ErrorType`** · `DomainError.StatusCode`→*removed* · per-product `FailureCategory`→shared **`ErrorType`** · thrown wrapper→**`ErrorException`** · `ValidationError`→keep (just emit `Code`).

---

## 6. Component inventory + when

| Component | Today | Action | When |
|---|---|---|---|
| `Error` value | `DomainError` (HTTP-baked) | rename + reshape | NOW |
| `ErrorType` enum | `DomainErrorCategory`+product `FailureCategory` | unify, drop int | NOW |
| base factories (`Error.NotFound`…) | on `DomainError` | re-home | NOW |
| `ErrorException` + bridge | ❌ | new | NOW |
| `ErrorType→HTTP` map | duplicated in products | DRY-lift to SDK | NOW |
| validation ProblemDetails | drops `code` | emit `code`+`errors[]` | NOW |
| conventions (3 docs) | contradictory | rewrite as one | NOW |
| general `ErrorExceptionHandler` + fallback 500 | ❌ | new | NEXT |
| auth → 403 | 500 bug | fix | NEXT |
| `AppResult.TFailure` → `Error`; `Result<T>` role | two error models | re-point + document | NEXT |
| app error catalogs (`OrderErrors.*`) | inline `new(...)` | convention + ref impl | NEXT |
| FE `code`/`errors[]` parse + per-field map | ❌ | new | NEXT |
| `Result`/`IValidator<T>`/FV adapter/`AddTraceAwareProblemDetails` | ✅ | keep | — |
| multi-error aggregation (`IReadOnlyList<Error>`) | single | decide D3 | LATER |
| i18n describer + FE label-map | ❌ | hooks ready NOW, fill | LATER |
| FE toast-for-errors std + `ErrorBoundary`/page | ad-hoc | standardize | LATER |
| error-code analyzer (format/uniqueness) | ❌ | new | LATER |

---

## 7. Open decisions

- **D1 (fork):** universal-`Error`-everywhere vs keep typed per-op `AppResult.Failure`. *Rec: hybrid — `Error` value, `AppResult` carrier.*
- **D2 carriers:** retire Foundation `Result<T>` vs keep as lightweight `Error`-carrier. *Rec: keep + document.*
- **D3 multi-error:** aggregate list vs single. *Rec: single now.*
- **D4 app categories:** `402`/`503`/`429` as base `ErrorType` vs app-extension. *Rec: add common ones to base.*
- **D5 `type` URI:** docs URL vs opaque URN. *Rec: URN now.*
- **D6 i18n shape:** default-string vs key-only. *Rec: default-string + always emit `code`.*
- **D7 doc home:** lives in `docs/planning/errors/`; on approval distill into the 3 conventions + `targets.md`.

---

## 8. Sources

MS: design-guidelines/exceptions · exceptions/best-practices · aspnet/core error-handling-api · aspnet/core localization. Repos/specs: github.com/error-or/error-or · github.com/altmann/FluentResults · rfc-editor.org/rfc/rfc9457. Blogs: milanjovanovic.tech (result pattern, ProblemDetails, global handling, CQRS+FluentValidation) · andrewlock.net (result pattern) · swagger.io (RFC 9457). Ardalis.Result / CSharpFunctionalExtensions / language-ext from knowledge. *(Harness verify was rate-limited; validated by hand against the above.)*
