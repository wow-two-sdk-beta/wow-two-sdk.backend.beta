# Errors, Results & Exceptions — architecture investigation

*Last updated: 2026-06-20 · Status: **investigation / no implementation** — hand-pick → backlog → build*

> **Goal of this doc.** A full investigation of the SDK's error / validation / exception layer: current state (codebase scan), external landscape (Microsoft + community + Result libs), and a recommended target design that answers every open question — then a **hand-pick split** (build NOW / NEXT / LATER) and the open decisions to confirm before any code.
>
> Builds on / supersedes the older design memory `docs/analysis/validation-and-result-pattern.md` and pairs with `docs/planning/mediator-cqrs/mediator-cqrs-result-absorption.md`. Conventions touched: `conventions/development/backend/foundation/result-pattern.md`, `.../foundation/validation.md`, `.../presentation/problem-details.md`.

---

## 0. Methodology & confidence

- **Codebase scan** — 3 parallel agents over the backend-beta SDK, products (drydock, secrets-vault, smart-qr, haven, your-pocket-doctor, prism), the UI lib, and the convention docs. Findings in §2 are read off the actual source (file:line cited).
- **External research** — fan-out web search (5 angles, 24 sources, 120 claims). ⚠️ The harness's adversarial *verify* phase was **rate-limited** (every voter returned `Server is temporarily limiting requests`), so it reported "0 confirmed" — that is an **infra artifact, not a refutation**. The extracted claims are standard, well-established facts (ErrorOr/FluentResults shapes, RFC 9457, ASP.NET Core ProblemDetails, MS guidelines); I validated each against primary sources + first-hand knowledge rather than the (failed) harness vote. Sources listed in §10.
- **Confidence:** current-state facts = high (read from source). External facts = high (primary docs/repos). Design recommendations = opinionated, flagged as decisions in §9.

---

## 1. TL;DR — the recommended model

**One error concept, two surfaces, one mapping seam.**

1. **`Error`** — a single, universal, transport-agnostic value (`Code` + `Description` + `ErrorType` + optional `Metadata`). Replaces `DomainError` (kill the name) **and** the per-product `FailureCategory` duplication. SDK ships a **base catalog** of `ErrorType`s + factories; apps layer **app-specific catalogs** of `Error`s on top.
2. **Two surfaces over the same `Error`:**
   - **Return it** → `Result` / `Result<T>` (and the mediator's `AppResult`) for the no-throw path.
   - **Throw it** → `ErrorException(Error)` for the throw path; the method returns the bare `T`.
   - A **bridge** (`error.Throw()`, `result.ValueOrThrow()`, `Result.Try(...)`) lets any call site switch between the two at will — that is the "switch between error and exception at any point" requirement.
3. **Mapping lives at the edge.** `Error` carries a *semantic* category usable at any layer (logging, branching, retries); the **category → HTTP status** translation happens **only at the presentation layer**, in one mapper, emitted as **RFC 9457 ProblemDetails** with the `code` and (for validation) an `errors[]` extension.
4. **i18n-ready, not i18n-now.** `Code` is the stable key; `Description` is the default (English) string. Emit `code` in every ProblemDetails so a future `IStringLocalizer` lookup (server) or label-map (frontend) can translate without touching call sites.

**Why now:** the SDK currently has **two competing failure models** (§2.1) and **drops the validation error `code`** on the wire (§2.4) — both block clean frontend handling and i18n. Unifying the `Error` model is high-value and largely mechanical.

---

## 2. Current state (codebase scan)

### 2.1 The core problem — two parallel, competing failure models

| | **Foundation model** | **Mediator model (the mandated one)** |
|---|---|---|
| Carrier | `Result` / `Result<T>` (`src/Foundation/Results/`) | `AppResult<TSuccess,TFailure>` (`src/Mediator/Result/AppResult.cs`) |
| Error shape | `DomainError(Code, Message, Category, Detail?)` (`src/Foundation/Errors/DomainError.cs`) | per-operation `{Op}Result.Failure` implementing product `I{App}Failure(ErrorMessage, FailureCategory)` |
| Category | `DomainErrorCategory` enum, **9 values, HTTP int baked in** (`Validation = 400`) | `FailureCategory` enum, **6 values, no int** — mapped by `ApiResults.ToStatusCode()` |
| HTTP mapping | `DomainError.StatusCode => (int)Category` (on the error) | `ApiResults.ToStatusCode(category)` (presentation, product-side) |
| Used by | **almost nothing** — orphan primitive | drydock, secrets-vault, smart-qr (the real products) |
| Convention | referenced by `validation.md` | mandated by `result-pattern.md` ("`Ok(dto)` banned ecosystem-wide") |

**The two adjacent convention files describe different failure models.** `result-pattern.md` says every handler returns `AppResult` + `FailureCategory`; `validation.md` says map a validation failure to `DomainError.Validation(...)` → `Result<T>.Fail(DomainError)`. Products follow the former and **never touch `DomainError`/`Result<T>`**. So the Foundation `Result`+`DomainError` pair is effectively dead code that the docs still point at — and `DomainError` is the name you want gone anyway.

Prior memory (`docs/analysis/validation-and-result-pattern.md`) shows this was a conscious sequence: a 3-variant `Error` union was sketched then **dropped** ("forced every failure check to branch on error-kind"), locking "validation and operation Result are separate channels; they don't convert." That decision is the root of today's split — and is worth revisiting now that the ask is a *single `Error` + bridge*, not a *union*.

### 2.2 "DomainError" naming — where it lives

Confined to Foundation (products already avoid it):
- `src/Foundation/Errors/DomainError.cs` — `DomainError` record + `DomainErrorCategory` enum.
- `src/Foundation/Results/Result.cs` + `ResultOfT.cs` — `Failure(DomainError Error)`, `Fail(DomainError)`, `Match(..., Func<DomainError,TOut>)`.
- `src/Foundation/Errors/errors.md` + `validation.md` — docs (and a stale `ErrorOr<User>` example in `errors.md` that doesn't match the code).

Rename blast radius is **small** (Foundation only) precisely because the mandated path went a different way.

### 2.3 Exceptions — ad-hoc, no hierarchy, no bridge

| Exception | File | Thrown by | Handler |
|---|---|---|---|
| `ValidationException(IReadOnlyList<ValidationError>)` | `src/Foundation/Validation/ValidationException.cs` | `ValidationBehavior` via `ValidateAndThrow` | ✅ `ValidationExceptionHandler` → 400 |
| `AuthorizationException(AuthorizationFailure?)` | `src/Mediator/Authorization/AuthorizationBehavior.cs` | `AuthorizationBehavior` | ❌ none → bubbles to 500 |
| `MigrationDriftException` / `MigrationOrphanException` | `src/Data/Migrations/Bespoke/` | migration validation | ❌ CLI-only, not HTTP |

- **No base exception type.** No `Error`-carrying exception. **No bridge** between `Error` ↔ exception (no `ThrowIfFailure`, no `Error.ToException()`). The two worlds (return vs throw) cannot interconvert today.
- `AuthorizationException` is orphaned — authenticated-but-forbidden becomes a 500, not a 403.

### 2.4 Mapping & ProblemDetails

- ✅ `AddTraceAwareProblemDetails` (`src/Web/ProblemDetails/`) wraps built-in `AddProblemDetails`, enriches every payload with `traceId` (`Activity.Current?.Id`) + `requestId` (`HttpContext.TraceIdentifier`). Good baseline.
- ✅ `ValidationExceptionHandler : IExceptionHandler` → `ValidationProblemDetails`, errors grouped by property.
- ❌ **`ValidationError.Code` is dropped on the wire** — only `Property` + `Message` reach the client (`problem-details.md:26`). The frontend therefore *cannot* map a failure to a stable key/field-rule → blocks i18n + per-field UX.
- ❌ **No general error→ProblemDetails handler.** Non-validation `Error`s only become HTTP via each controller's `.Match(... onFailure: Problem(...))` using product `ApiResults.ToStatusCode`. Anything thrown that isn't `ValidationException` → unmapped 500.
- **Split brain on "where mapping lives":** `DomainError` bakes the int status into the *error* (any layer); the convention + `AppResult` keep it a *presentation* concern. These disagree.

### 2.5 Validation

- FluentValidation behind SDK `IValidator<T>` (`Validate` no-throw / `ValidateAndThrow`), `FluentValidationAdapter<T>`, `AddFluentValidatorsFromAssemblies`. Invoked by mediator `ValidationBehavior` (`ValidateAndThrow`). Clean, swappable, keep as-is.
- `Guard.Against` (Ardalis) for internal preconditions (throws `ArgumentException`) — distinct from boundary validation. Keep.
- Gap: validation produces `IReadOnlyList<ValidationError>` but operation `Result` carries a **single** `DomainError` — no multi-error aggregation across the two channels.

### 2.6 Cross-repo reality (de-facto patterns)

| Repo | Result carrier | Error shape | Mapping | Notes |
|---|---|---|---|---|
| **backend-beta SDK** | `Result<T>` + `AppResult<,>` | `DomainError` / typed failures | both (split) | the two-model source |
| **drydock** | SDK `AppResult` | `IDrydockFailure` + `FailureCategory(6)` | `ApiResults.ToStatusCode` | reference impl |
| **secrets-vault** | SDK `AppResult` | `ISecretsVaultFailure` + `FailureCategory(+Unavailable 503)` | `ApiResults.ToStatusCode` | extends enum |
| **smart-qr** | SDK `AppResult` | `ISmartQrFailure` + `FailureCategory(+PaymentRequired 402)` | `ApiResults.ToStatusCode` | extends enum |
| **haven** | custom `ApplicationResult<,>` | per-op typed failures + context | TBD | pre-absorption copy |
| **your-pocket-doctor** | ❌ exceptions | `AppException(HttpStatusCode, msg)` hierarchy + `ExceptionFormatter` middleware | status baked on exception | **pre-SDK; cautionary** — the "everything throws, status on the exception" extreme |
| **prism** | n/a (frontend only) | — | — | — |

**Reads:** (a) `FailureCategory` is duplicated in every product with small per-app deltas (`Unavailable`, `PaymentRequired`) — a textbook DRY-lift-to-SDK candidate (`result-pattern.md:116` already flags it as a "Future option"); (b) `your-pocket-doctor` shows the anti-pattern to avoid — HTTP status welded onto exception types, decentralized mapping; (c) every .NET product uses FluentValidation — safe to standardize hard.

### 2.7 Frontend error UX (today)

- **Transport:** each frontend has `api/client.ts` with an `ApiError { status, problem: ProblemDetails|null }`, parsing RFC 7807 and using `problem.detail ?? problem.title` as the message. Consistent across drydock + sift.
- **Display surfaces that exist:** `@wow-two-beta/ui` ships `Alert` (static, inline, `severity: info|success|warning|danger|neutral`) and a global `Toaster` singleton (`toaster.toast({...})`). Hooks expose `{ data, loading, error: string|null }`.
- **Gaps:** no **error boundary** / dedicated error route; **no per-field server-error mapping** (forms only show submit-level message because `code`/`property` don't arrive usefully); toasts used for success/info but **not** standardized for errors; secrets-vault hand-rolls `ErrorBanner` instead of `Alert`; **no i18n** anywhere (plain English from the server).

---

## 3. External landscape (Microsoft + community)

### 3.1 Throw vs return — the consensus

- **Microsoft framework design guidelines**: exceptions are the *default* error-reporting mechanism for member contracts; **do not** return error codes; do **not** avoid exceptions for performance.[¹] For routinely-expected failures, the sanctioned non-throwing forms are **Try-Parse** (`bool TryX(out T)`) and **Tester-Doer**.[¹][²]
- **Community (Milan Jovanović, Andrew Lock, et al.)**: the **Result pattern** is the modern idiom for *expected, domain* failures — exceptions are for *exceptional/unexpected* conditions and control-flow-by-exception is an anti-pattern (cost + hidden flow).[³][⁴]
- **Synthesis for us:** expected/known failures → **`Error` via Result**; programmer errors + truly unexpected → **throw**. The SDK should make *both* first-class and **convertible**, which satisfies both the MS guidance (Try-style + throw available) and the community idiom (Result default). This is exactly the user's "method can return Result, or throw with a different return value."

### 3.2 Result libraries — shape comparison

| Lib | Error shape | Multi-error | Codes | Implicit conv. | Notes |
|---|---|---|---|---|---|
| **ErrorOr**[⁵] | `Error(Code, Description, Type, NumericType, Metadata?)`; `ErrorType` enum (Failure, Unexpected, Validation, Conflict, NotFound, Unauthorized, Forbidden) + `Error.Custom` | ✅ `List<Error>` (`FirstError`, `ErrorsOrEmptyList`) | ✅ first-class `Code` | ✅ `T`/`Error`→`ErrorOr<T>` | **closest to the target model**; ergonomic, popular |
| **FluentResults**[⁶] | `Result` / `Result<T>`; `IError`/`ISuccess` reasons | ✅ `Errors`, `Reasons`, `Successes` | ⚠️ via metadata or subclass (not first-class) | partial | rich "reasons" graph; heavier |
| **Ardalis.Result** | `Result<T>` + `ResultStatus` enum (Ok, NotFound, Invalid, Unauthorized, Forbidden, Conflict, …) + `ValidationError[]` | ✅ validation errors | ⚠️ via `ValidationError.ErrorCode` | some | maps to HTTP via `Ardalis.Result.AspNetCore`; status as enum (our `FailureCategory` is basically this) |
| **CSharpFunctionalExtensions** | `Result<T,E>` with generic error `E` | ❌ (single `E`, you choose its shape) | depends on `E` | yes | most functional; bring-your-own error |
| **language-ext** | `Fin<T>` / `Either<L,R>`, `Error` | varies | via `Error.Code` | yes | full FP; heaviest, steep curve |

**Takeaways:** ErrorOr is the template — `Code + Description + Type` is precisely the user's "base error codes with descriptions" + "switch to exception." Ardalis confirms the `ResultStatus`/`FailureCategory` enum-to-HTTP approach we already use. We don't have to *take a dependency* (the SDK is "beta-forever, MIT, no commercial deps; source-gen first") — we can ship our own `Error`/`Result` modeled on ErrorOr's shape, which is what the codebase already started.

### 3.3 ProblemDetails (RFC 9457) + ASP.NET Core

- **RFC 9457** (obsoletes 7807) is the standard error format; media type `application/problem+json`; core members `type`(URI), `title`, `status`, `detail`, `instance`; **extensible** with custom members (e.g. `code`, `errors[]`) and clients must ignore unknown ones.[⁷][⁸] The `type` URI is the machine-readable key consumers should branch on (not `title`).[⁷]
- **ASP.NET Core**: `AddProblemDetails()` registers `IProblemDetailsService`; `IExceptionHandler` is the modern global-handler seam (chainable, replaces custom middleware); `ProblemDetailsOptions.CustomizeProblemDetails` enriches centrally.[⁹] We already use all three — the gap is *coverage* (only validation) + emitting `code`.

### 3.4 Validation & i18n

- **FluentValidation > DataAnnotations** for non-trivial rules (DI-resolved, composable, testable) — community + our convention agree; keep FluentValidation behind `IValidator<T>`.[³]
- **i18n**: ASP.NET Core localizes via `IStringLocalizer<T>` over `.resx`, returning the **key string as fallback** when no translation exists — so a *message-key-as-default* workflow needs no default `.resx`.[¹⁰] This is the groundwork hook: make `Error.Code` the key; resolve later.

---

## 4. Target design — answering the brief

### 4.1 Two concepts: `Error` and `Exception` (shape & declaration)

**`Error`** — the universal value (replaces `DomainError`). Illustrative:

```csharp
namespace WoW.Two.Sdk.Backend.Beta.Errors;

/// <summary>A transport-agnostic, expected failure. Returned via Result or thrown via ErrorException.</summary>
public sealed record Error(
    string Code,                 // stable key, dotted: "orders.not_found" — also the i18n key
    string Description,          // default human-readable message (English for now)
    ErrorType Type,              // semantic category (NOT an HTTP int)
    IReadOnlyDictionary<string, object?>? Metadata = null)
{
    public Error WithMetadata(string key, object? value) => /* copy-add */;
    public ErrorException ToException() => new(this);   // bridge → throw
    public void Throw() => throw ToException();
}

/// <summary>Base, transport-agnostic category. HTTP mapping happens at the edge, not here.</summary>
public enum ErrorType
{
    Failure = 0,   // generic expected failure
    Validation, Unauthorized, Forbidden, NotFound, Conflict,
    BusinessRule, TooManyRequests, Unavailable, Unexpected
}
```

Note vs today: **no `StatusCode` property on the error** (that was the mapping smell). `ErrorType` is semantic; the int lives in one presentation mapper (§4.4).

**Exception** — a base type that *carries* an `Error`, plus a shallow hierarchy:

```csharp
/// <summary>The throw-surface of an Error.</summary>
public class ErrorException(Error error) : Exception(error.Description)
{
    public Error Error { get; } = error;
}

// Optional thin specializations for catch-site convenience / existing call sites:
public sealed class ValidationException(IReadOnlyList<Error> errors)
    : ErrorException(errors[0]) { public IReadOnlyList<Error> Errors { get; } = errors; }
```

How they're **declared** in app code — two styles, author's choice per method:

```csharp
// catalog: app-specific errors layered on the base (see §4.3)
public static class OrderErrors
{
    public static Error NotFound(Guid id) =>
        new("orders.not_found", $"Order {id} was not found.", ErrorType.NotFound);
}

// return style (no throw):
public Result<Order> Get(Guid id) =>
    _repo.Find(id) is { } o ? Result.Ok(o) : Result.Fail(OrderErrors.NotFound(id));

// throw style (different return value — bare Order):
public Order GetOrThrow(Guid id) =>
    _repo.Find(id) ?? throw OrderErrors.NotFound(id).ToException();
```

### 4.2 Switchability — the bridge (return ⇄ throw, at any point)

The defining requirement. Combinators on the `Result`/`Error`/exception triangle:

| From → To | API | Use |
|---|---|---|
| `Error` → throw | `error.Throw()` / `throw error.ToException()` | opt into throwing |
| `Result<T>` → value-or-throw | `T value = result.ValueOrThrow();` | consume a Result in throwing code |
| `Result` → throw-if-failed | `result.ThrowIfFailure();` | guard then continue |
| throwing → `Result<T>` | `Result.Try(() => Dangerous());` (catches `ErrorException` → `Fail`, rethrows the rest) | wrap a throwing API as a Result |
| exception → `Error` | `ex is ErrorException ee ? ee.Error : Error.Unexpected(...)` | boundary mapping (§4.5) |

This lets a low-level method return `Result<T>`, a caller `.ValueOrThrow()` into exception-style code, and the global handler convert any leaked `ErrorException` back into a mapped response — the same `Error` flowing through all three forms. (This is the *bridge* the earlier design lacked, and is strictly simpler than the rejected 3-variant union — one type, convertible, no branch-on-kind.)

### 4.3 Error types — base catalog + app-specific catalog

- **SDK base** ships `ErrorType` + factory helpers for the common shapes: `Error.NotFound(code, desc)`, `Error.Validation(...)`, `Error.Conflict(...)`, `Error.Unexpected(...)`, etc. (the `DomainError.*` factories, re-homed onto `Error`).
- **Apps layer** static **catalogs** of concrete `Error`s with stable codes (`OrderErrors.NotFound`, `BillingErrors.PlanCapReached`) — discoverable, greppable, the single place a code+message is defined. This replaces today's inline `new(...)` at call sites *and* the per-product `FailureCategory` copy (the category is now the shared SDK `ErrorType`; app extensions like `PaymentRequired` become either a new base `ErrorType` value or app metadata — see Decision D4).
- **Codes are conventionised**, not typed: `{area}.{snake_case}` (matches existing `orders.not_found`). Optionally a Roslyn analyzer later to enforce uniqueness/format (SDK already plans analyzers).

### 4.4 Where mapping happens — the decisive answer

> **Recommendation: the error carries *semantic* category at every layer; the *HTTP* mapping happens only at the presentation edge, in exactly one place.**

- ✅ `Error.Type` (semantic) is available at *any* layer — for logging, metrics, retry decisions, branching. (So "can the error carry it at any layer?" — yes, the *meaning*.)
- ❌ The **HTTP status int is not on the error** (kill `DomainError.StatusCode`). Translating `ErrorType` → status code is a transport concern; an HTTP error and a queue-consumer error share the same `Error` but map differently.
- **One mapper, SDK-owned:** `ErrorType → HTTP status` (`ErrorTypeHttpMap`), DRY-lifted from the duplicated product `ApiResults.ToStatusCode`. Apps override only deltas.
- **One emit path:** controller `.Match(onFailure: e => Problem(...))` *or* (better, §4.5) a global handler turns any `Error`/`ErrorException` into RFC 9457 ProblemDetails — always including `code`, plus `errors[]` for validation.

This keeps the layering honest (domain/app stays transport-agnostic; only `Web` knows HTTP) while still letting every layer *read* the category.

### 4.5 Cross-cutting: global handler, ProblemDetails, frontend display

**Server — global pipeline (extend, don't replace):**
1. Keep `AddTraceAwareProblemDetails` (traceId/requestId).
2. Keep `ValidationExceptionHandler` **but emit `code`** → add `errors` as an RFC 9457 extension: `{ property, code, message }[]` (not just grouped messages).
3. **Add a general `ErrorExceptionHandler : IExceptionHandler`** — maps any `ErrorException` → ProblemDetails via the `ErrorType` map, emits `type` (URI from code), `title`, `status`, `detail`, `code`. Chains after validation.
4. **Add a fallback handler** — unmapped exceptions → 500 ProblemDetails, `code = "errors.unexpected"`, no internal leakage (detail generic in prod, full in dev via developer exception page).
5. Map `AuthorizationException` → 403 (fix the current 500 bug).

**ProblemDetails contract (the wire shape every client can rely on):**

```jsonc
{
  "type": "https://errors.wow-two.dev/orders.not_found",
  "title": "Not Found",
  "status": 404,
  "detail": "Order 3f2… was not found.",
  "code": "orders.not_found",        // ← stable key (NEW; today it's dropped)
  "traceId": "...", "requestId": "...",
  "errors": [                         // ← validation only
    { "property": "email", "code": "NotEmptyValidator", "message": "Email is required." }
  ]
}
```

**Frontend — display-surface decision matrix** (what shows where):

| Failure kind (by `ErrorType` / status) | Surface | Component | Rationale |
|---|---|---|---|
| **Validation (400 + `errors[]`)** | **inline, per-field** in the form | map `errors[].property`→field; react-hook-form `setError` | user fixes input in place |
| **Form-level domain failure** (409 Conflict, 422 BusinessRule) on a submit | **form modal status** / inline `Alert severity="danger"` at form top | `Alert` | tied to the action the user just took |
| **Transient action failure** (network blip, 503, optimistic mutation) | **toast** | `Toaster` (`severity="danger"`) | non-blocking, dismissible |
| **NotFound (404)** on a *route/page* load | **dedicated state/route** (empty/404 panel) | route-level not-found | the whole view has no data |
| **Unexpected (500) / unhandled render error** | **error boundary → error page** | React `ErrorBoundary` (NEW) + fallback page | last-resort, prevents white screen |
| **Auth (401)** | redirect to login (interceptor) | api client | session concern, not a toast |

Principle: **toast for transient/ambient, inline/field for the form the user is in, page/boundary for whole-view failures.** Today only `Alert` (inline) is wired; toast-for-errors, per-field mapping, and an error boundary are the additions.

### 4.6 i18n groundwork (build the hooks, not the translations)

Establish now so translation is a later drop-in:
- **`Error.Code` is the stable key** — never localize the code, only the description.
- **Always emit `code`** in ProblemDetails (server) — the single change that unblocks both server- and client-side translation later.
- **Server hook (later):** an `IErrorDescriber`/`IStringLocalizer` lookup keyed by `Code` resolves `Description` per `Accept-Language`; absent a resource, the default string is returned (`IStringLocalizer` already does this).[¹⁰] No call-site changes — factories keep passing the default string.
- **Frontend hook (already idiomatic):** the existing enum **label-`Record`** pattern (`conventions/.../frontend/enums.md`) is the translation bridge; an `errorCode → message` map mirrors it. With `code` on the wire, the client can translate without the server.
- **Decision to defer (D6):** keep `Description` as default-string now (simplest) vs. make it key-only immediately. Recommend default-string + always-emit-code.

---

## 5. Reconciliation with the existing `AppResult` convention

The mandated convention (`result-pattern.md`) and the in-flight absorption (`mediator-cqrs-result-absorption.md`) are heavily invested in `AppResult<TSuccess,TFailure>` + per-operation typed failures + per-product `FailureCategory`. The new `Error` model must **fit**, not fight:

- **Unify the error, keep the carrier.** Recommend: standardize `AppResult`'s `TFailure` on the universal **`Error`** (or a failure that *carries* an `Error`), and replace per-product `FailureCategory` with the SDK `ErrorType` + one map. This kills the duplication and the second error type **without** ripping out the freshly-mandated `AppResult` or its context channels.
- **Collapse the orphan.** The Foundation `Result`/`Result<T>` + `DomainError` pair is barely used. Options: (a) retire it, route everything through `AppResult` + `Error`; or (b) keep `Result<T>` as the lightweight non-mediator primitive **carrying the same `Error`** (for library/foundation code with no `ISuccessResult`/context needs). Recommend (b) — one error model, two carriers with clearly documented use (lightweight vs context-bearing), which is *much* less confusing than today's two *error* models.
- **The central fork (Decision D1):** universal-`Error`-everywhere (ErrorOr-style, your #2 lean) vs keep typed-per-operation `Failure` payloads. Recommend the **hybrid**: universal `Error` as the *failure value*, `AppResult` as the *carrier* where context/typed-success matters — get the simplicity of one `Error` and keep the exhaustive success/failure split the convention prizes.

---

## 6. Naming — kill "DomainError"

| Today | Proposed | Note |
|---|---|---|
| `DomainError` | **`Error`** | matches ErrorOr/community; the user's explicit ask |
| `DomainErrorCategory` | **`ErrorType`** | semantic category, no HTTP int |
| `DomainError.StatusCode` | *(removed)* | mapping moves to presentation |
| per-product `FailureCategory` | **`ErrorType`** (SDK, shared) | de-dupe |
| `I{App}Failure` (ErrorMessage+Category) | `TFailure : carries Error` | one shape |
| `ValidationError(Property, Message, Code)` | keep | already fine; just **emit `Code`** |
| thrown wrapper | **`ErrorException`** | new bridge type |

> Namespace today is `...Foundation.Errors` (code) but `errors.md` advertises package `...Errors` and even shows a stray `ErrorOr<User>` example — reconcile naming/namespace when renaming.

---

## 7. Component inventory — everything the layer needs

| # | Component | Status today | Action |
|---|---|---|---|
| 1 | `Error` value (Code, Description, Type, Metadata) | ⚠️ `DomainError` (HTTP-baked) | **rename + reshape** |
| 2 | `ErrorType` base enum | ⚠️ `DomainErrorCategory`(HTTP int) + product `FailureCategory` | **unify, drop int** |
| 3 | Base error factories (`Error.NotFound`…) | ✅ on `DomainError` | re-home onto `Error` |
| 4 | App error catalogs (`OrderErrors.*`) | ❌ inline `new(...)` | **convention + examples** |
| 5 | `Result` / `Result<T>` | ✅ (orphan) | re-point to `Error`; clarify role |
| 6 | `AppResult<TSuccess,TFailure>` | ✅ mandated | re-point `TFailure` to `Error` |
| 7 | Multi-error aggregation | ❌ single error | **decide** (D3) `IReadOnlyList<Error>` |
| 8 | `ErrorException` (carries `Error`) | ❌ | **new** |
| 9 | Bridge (`Throw`,`ValueOrThrow`,`ThrowIfFailure`,`Result.Try`) | ❌ | **new** |
| 10 | `ValidationError` + `IValidator<T>` + FV adapter | ✅ | keep |
| 11 | `ValidationException` | ✅ | re-base on `ErrorException`; **emit `code`** |
| 12 | `ErrorType → HTTP` map | ⚠️ duplicated in products | **DRY-lift to SDK** |
| 13 | `AddTraceAwareProblemDetails` | ✅ | keep |
| 14 | `ValidationExceptionHandler` | ✅ | **add `errors[]` w/ code** |
| 15 | `ErrorExceptionHandler` (general) | ❌ | **new** |
| 16 | Fallback 500 handler | ⚠️ framework default | **new, code+no-leak** |
| 17 | Auth → 403 mapping | ❌ (500 bug) | **fix** |
| 18 | ProblemDetails `code`/`type` URI convention | ❌ | **new** |
| 19 | i18n hook (`IErrorDescriber`/localizer by code) | ❌ | **stub interface only** |
| 20 | FE `ApiError` + ProblemDetails parse | ✅ | extend for `code`/`errors[]` |
| 21 | FE per-field error mapping | ❌ | **new** |
| 22 | FE toast-for-errors convention | ⚠️ ad-hoc | **standardize** |
| 23 | FE `ErrorBoundary` + error page | ❌ | **new** |
| 24 | FE error label-map (i18n bridge) | ❌ | **defer, hook ready** |
| 25 | Docs: reconcile `result-pattern.md`/`validation.md`/`problem-details.md` | ⚠️ contradictory | **rewrite together** |

---

## 8. Hand-pick — NOW / NEXT / LATER

**NOW (core unification — high value, mostly mechanical):**
- [1] Rename `DomainError`→`Error`, `DomainErrorCategory`→`ErrorType`; remove `StatusCode` from the error.
- [8][9] Add `ErrorException` + the bridge (`Throw`/`ValueOrThrow`/`ThrowIfFailure`/`Result.Try`).
- [12] DRY-lift `ErrorType → HTTP` map into the SDK.
- [11][14] Emit `code` + `errors[]` in validation ProblemDetails.
- [25] Rewrite the 3 convention docs as one coherent story (decide D1 first).

**NEXT (close the HTTP + correctness gaps):**
- [15][16] General `ErrorExceptionHandler` + fallback 500 handler.
- [17] Auth → 403 fix.
- [6] Re-point `AppResult.TFailure` to `Error`; [5] clarify `Result<T>` role (D1/D2).
- [4] App error-catalog convention + reference impl in drydock.
- [20][21] Frontend: parse `code`/`errors[]`, per-field mapping.

**LATER (backlog):**
- [7] Multi-error aggregation (if D3 = yes).
- [19][24] i18n: server `IErrorDescriber` + FE label-map (hooks already in place from NOW).
- [22][23] FE toast-for-errors standardization + `ErrorBoundary`/error page.
- Roslyn analyzer: error-code format/uniqueness; foundation-can't-import-domain enforcement.

---

## 9. Open decisions (confirm before building)

- **D1 — central fork:** universal-`Error`-everywhere vs keep typed-per-operation `AppResult.Failure`. *Rec: hybrid — `Error` as failure value, `AppResult` as carrier.*
- **D2 — carriers:** retire Foundation `Result<T>` vs keep it as the lightweight `Error`-carrying primitive. *Rec: keep, document the split.*
- **D3 — multi-error:** does `Result`/`Error` aggregate `IReadOnlyList<Error>` (ErrorOr-style) or stay single? *Rec: single now, list later if needed.*
- **D4 — app categories:** `PaymentRequired`/`Unavailable` etc. as new base `ErrorType` values vs app-defined extensions. *Rec: add the common ones (402/503/429) to the base enum; rare ones via metadata.*
- **D5 — `type` URI:** real dereferenceable docs URL vs opaque URN (`urn:wow-two:error:orders.not_found`). *Rec: opaque URN now, docs URL later.*
- **D6 — i18n shape:** `Description` as default-string vs key-only. *Rec: default-string + always emit `code`.*
- **D7 — doc home:** this lives in `docs/planning/errors/`; on approval, distill into the 3 conventions + `targets.md` entry.

---

## 10. Sources

Primary: MS exceptions design guidelines[¹] · MS exception best-practices[²] · MS error-handling API / ProblemDetails[⁹] · MS localization (`IStringLocalizer`)[¹⁰] · ErrorOr repo[⁵] · FluentResults repo[⁶] · RFC 9457[⁷]. Secondary/blog: Milan Jovanović (Result pattern[³], ProblemDetails, global error handling, CQRS+FluentValidation) · Andrew Lock (Result pattern as control flow)[⁴] · Swagger RFC 9457[⁸] · codingdroplets (ErrorOr vs OneOf vs FluentResults). Ardalis.Result / CSharpFunctionalExtensions / language-ext from first-hand knowledge.

[¹] learn.microsoft.com/dotnet/standard/design-guidelines/exceptions
[²] learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions
[³] milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern
[⁴] andrewlock.net/working-with-the-result-pattern-part-1-replacing-exceptions-as-control-flow/
[⁵] github.com/error-or/error-or
[⁶] github.com/altmann/FluentResults
[⁷] rfc-editor.org/rfc/rfc9457.html
[⁸] swagger.io/blog/problem-details-rfc9457-api-error-handling/
[⁹] learn.microsoft.com/aspnet/core/fundamentals/error-handling-api
[¹⁰] learn.microsoft.com/aspnet/core/fundamentals/localization

> ⚠️ The research harness's automated claim-verification was rate-limited (§0); citations above were confirmed against primary sources + first-hand knowledge, not the harness's (failed) adversarial vote.
