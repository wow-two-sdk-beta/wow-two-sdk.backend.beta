# Errors, Results & Exceptions — architecture

*Last updated: 2026-06-22 · Status: **design converged** (model + carriers + bridge locked) · mapping/ProblemDetails being detailed · Implementation = §8 (fill on lock)*

> Redesign of the SDK error / validation / exception layer. Builds on `docs/analysis/validation-and-result-pattern.md`; pairs with `docs/planning/mediator-cqrs/mediator-cqrs-result-absorption.md`. Conventions to rewrite on approval: `foundation/result-pattern.md`, `foundation/validation.md`, `presentation/problem-details.md`.
>
> **Scope of investigation:** scanned the SDK, apps (drydock, secrets-vault, smart-qr, haven, your-pocket-doctor, prism), the UI lib + conventions; external = MS docs, ErrorOr / FluentResults / Ardalis, RFC 9457. Web verify step was rate-limited → external claims validated by hand vs primary sources (§9).
>
> **One-line model:** one `AppError` (enum `Type` + `Message` + `Metadata` + `Origin`) → **return** it (`Result` / `AppResult`) or **throw** it (`AppException`), bridged both ways; status/translation mapped at the edge via DI seams.

---

## 1. Problem & current state

**Two competing failure models exist today:**

| | Foundation (orphan) | Mediator (mandated, real) |
|---|---|---|
| Carrier | `Result`/`Result<T>` | `AppResult<TSuccess,TFailure>` |
| Error | `DomainError(Code,Message,Category,Detail?)` | per-op `Failure : I{App}Failure(ErrorMessage,FailureCategory)` |
| Category | `DomainErrorCategory` (9, **HTTP int baked in**) | `FailureCategory` (6, no int) |
| HTTP map | `DomainError.StatusCode` (on the error) | `ApiResults.ToStatusCode` (presentation) |
| Used by | ~nothing | drydock, secrets-vault, smart-qr |

`result-pattern.md` and `validation.md` document *different* models; products never touch `DomainError`. A 3-variant `Error` **union** was tried and dropped (branch-on-kind) — the new direction is a **single `AppError` + a bridge**, not a union.

**Gaps / bugs:**
- `ValidationError.Code` **dropped on the wire** (`problem-details.md:26`) → blocks i18n + per-field form UX.
- Only `ValidationException` is mapped; no general error→ProblemDetails. `AuthorizationException` → **500 not 403**.
- `FailureCategory` duplicated per product (`result-pattern.md:116` flags the DRY-lift) with deltas (`Unavailable` 503, `PaymentRequired` 402).
- `your-pocket-doctor` = the anti-pattern (pre-SDK): everything throws, status welded onto `AppException`.
- FE: `ApiError`+ProblemDetails parse + `Alert`/`Toaster` exist; **no** per-field mapping, error boundary/page, toast-for-errors standard, or i18n.

---

## 2. External landscape (informing the design)

- **Throw vs return** — MS: exceptions are the default, but `Try-Parse`/`Tester-Doer` for routine failures; community: Result for *expected* failures, throw for *exceptional*. → make both first-class **and convertible**.
- **Result libs** — **ErrorOr** (`Error(Code,Description,Type,Metadata)` + 7-type enum) is the shape template; Ardalis confirms the enum→HTTP approach; FluentResults/CSharpFunctionalExtensions/language-ext heavier. → ship our own ErrorOr-shaped type (no commercial dep; SDK is beta-forever / source-gen-first).
- **ProblemDetails** — RFC 9457 (obsoletes 7807), `application/problem+json`, core `type/title/status/detail/instance`, **extensible** (`code`, `errors[]`). ASP.NET Core `AddProblemDetails`+`IProblemDetailsService`+`IExceptionHandler` already used — gap is *coverage* + emitting `code`.
- **Validation/i18n** — keep FluentValidation behind `IValidator<T>`; `IStringLocalizer` returns the key as fallback → message-key-as-default needs no `.resx`.

---

## 3. Design — by layer (dependency order)

> Code blocks below are **apply-ready** (house conventions: file-scoped namespaces, body-property `{ get; init; }` records with per-member `<summary>`, block bodies, summary starter words). Filename precedes each block.

### 3.1 Models — `AppError`, `AppErrorType`, `AppException`, validation types

Decisions: one open `record AppError` (open because `ValidationError` subclasses it); `AppErrorType` = SDK-owned abstract kind (name is the wire contract); no HTTP int / no app-specific codes (specificity = `Message` + `Origin` + `Metadata`); `Origin` = call-site, log-only; `AppException` = single carrier, subtype only for catch-read members; `Guard`/`ArgumentException` stay separate.

**`src/Foundation/Errors/AppErrorType.cs`**
```csharp
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the abstract kind of an <see cref="AppError"/> — the classifier mapped to a transport status at the edge.</summary>
public enum AppErrorType
{
    /// <summary>Unexpected server-side failure with no more specific kind.</summary>
    Unexpected,

    /// <summary>Caller-supplied input failed validation.</summary>
    Validation,

    /// <summary>The requested resource does not exist.</summary>
    NotFound,

    /// <summary>The request conflicts with the current state.</summary>
    Conflict,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not permitted.</summary>
    Forbidden,

    /// <summary>The caller exceeded a rate limit.</summary>
    TooManyRequests,

    /// <summary>A database operation exceeded its time budget.</summary>
    DbTimeout,

    /// <summary>An in-process operation exceeded its time budget.</summary>
    OperationTimeout,

    /// <summary>An external dependency rejected our credentials.</summary>
    ExternalUnauthorized,

    /// <summary>An external dependency is unreachable or unavailable.</summary>
    ExternalUnavailable,

    /// <summary>A required file was not found.</summary>
    FileNotFound,

    /// <summary>Serialization or deserialization failed.</summary>
    SerializationFailed,
}
```

**`src/Foundation/Errors/ErrorOrigin.cs`** — captured via caller-info (return path) or derived from an exception's stack (catch path); `File`/`Line` are nullable because the from-exception path needs PDBs:
```csharp
using System.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents the source location where an <see cref="AppError"/> was created — diagnostics only, never serialized to a client.</summary>
public sealed record ErrorOrigin
{
    /// <summary>Gets the member that created the error.</summary>
    public required string Member { get; init; }

    /// <summary>Gets the source file path, when available (requires debug symbols).</summary>
    public string? File { get; init; }

    /// <summary>Gets the source line, when available (requires debug symbols).</summary>
    public int? Line { get; init; }

    /// <summary>Builds an origin from the throw site of <paramref name="exception"/> — member always, file and line when symbols are present.</summary>
    /// <param name="exception">The exception whose deepest stack frame locates the throw site.</param>
    public static ErrorOrigin FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var frame = new StackTrace(exception, fNeedFileInfo: true).GetFrame(0);
        var member = frame?.GetMethod()?.Name ?? exception.TargetSite?.Name ?? "unknown";

        return new ErrorOrigin { Member = member, File = frame?.GetFileName(), Line = frame?.GetFileLineNumber() };
    }
}
```

**`src/Foundation/Errors/AppError.cs`** — a **pure value**: it never holds a live `Exception`. A caught exception is folded in via `FromException` (derives `Origin` from its stack + records the cause's type name); the live exception rides on `AppException.InnerException` when thrown, and is logged at the catch site:
```csharp
using System.Runtime.CompilerServices;
using WoW.Two.Sdk.Backend.Beta.Foundation.Guards;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents an expected, transport-agnostic failure — returned via a result or thrown via <see cref="AppException"/>.</summary>
public record AppError
{
    /// <summary>Gets the abstract kind of the failure.</summary>
    public required AppErrorType Type { get; init; }

    /// <summary>Gets the human-readable, default-culture message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the optional message-template arguments and diagnostic context surfaced as ProblemDetails extensions.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>Gets the source location that created the error.</summary>
    public ErrorOrigin? Origin { get; init; }

    /// <summary>Creates an error of <paramref name="type"/>, capturing the call site into <see cref="Origin"/>.</summary>
    /// <param name="type">The failure kind.</param>
    /// <param name="message">The default-culture message.</param>
    /// <param name="metadata">Optional message arguments and diagnostic context.</param>
    public static AppError Of(
        AppErrorType type,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        Guard.Against.NullOrWhiteSpace(message);

        var origin = new ErrorOrigin { Member = member, File = file, Line = line };

        return new AppError { Type = type, Message = message, Metadata = metadata, Origin = origin };
    }

    /// <summary>Creates an error from a caught <paramref name="exception"/>, deriving <see cref="Origin"/> from its throw site; the live exception is not retained.</summary>
    /// <param name="type">The failure kind.</param>
    /// <param name="message">The default-culture message.</param>
    /// <param name="exception">The caught exception whose stack and type are folded in.</param>
    public static AppError FromException(AppErrorType type, string message, Exception exception)
    {
        Guard.Against.NullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(exception);

        var metadata = new Dictionary<string, object?>
        {
            ["cause"] = exception.GetType().Name,
            ["causeChain"] = ExceptionChain.Flatten(exception),   // log-only; never auto-emitted to the wire
        };

        return new AppError { Type = type, Message = message, Metadata = metadata, Origin = ErrorOrigin.FromException(exception) };
    }

    /// <summary>Wraps the error in an <see cref="AppException"/> for the throw path, preserving <paramref name="inner"/> natively.</summary>
    /// <param name="inner">Optional underlying cause to preserve on the exception.</param>
    public AppException ToException(Exception? inner = null)
    {
        return new AppException(this, inner);
    }

    /// <summary>Throws this error as an <see cref="AppException"/>.</summary>
    /// <param name="inner">Optional underlying cause to preserve on the exception.</param>
    public void Throw(Exception? inner = null)
    {
        throw ToException(inner);
    }
}
```

**`src/Foundation/Errors/ExceptionChain.cs`** — flattens an inner-exception chain for diagnostics (the return-path equivalent of what `InnerException` preserves natively on throw):
```csharp
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Provides a flattened, depth-capped view of an exception's inner chain for diagnostics.</summary>
public static class ExceptionChain
{
    private const int MaxDepth = 5;

    /// <summary>Flattens <paramref name="exception"/> and its inner chain into "{Type}: {Message}" entries.</summary>
    /// <param name="exception">The exception to flatten.</param>
    public static IReadOnlyList<string> Flatten(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var entries = new List<string>();
        var current = exception;

        while (current is not null && entries.Count < MaxDepth)
        {
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    entries.Add($"{inner.GetType().Name}: {inner.Message}");
                }

                break;
            }

            entries.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }

        return entries;
    }
}
```

**`src/Foundation/Errors/AppException.cs`**
```csharp
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents the thrown form of an <see cref="AppError"/> — the single exception the global handler maps; subclass only to carry members a catch site reads.</summary>
public class AppException : Exception
{
    /// <summary>Initializes a new instance carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error being thrown.</param>
    /// <param name="inner">Optional underlying cause.</param>
    public AppException(AppError error, Exception? inner = null)
        : base(error.Message, inner)
    {
        Error = error;
    }

    /// <summary>Gets the error this exception carries.</summary>
    public AppError Error { get; }
}
```

**`src/Foundation/Errors/AppErrors.cs`** — SDK base catalog (apps add their own; caller-info threads through so `Origin` is the business call site):
```csharp
using System.Runtime.CompilerServices;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Provides factory helpers for the SDK's base <see cref="AppError"/> kinds.</summary>
public static class AppErrors
{
    /// <summary>Creates a <see cref="AppErrorType.NotFound"/> error.</summary>
    public static AppError NotFound(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.NotFound, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.Unexpected"/> error; when an <paramref name="inner"/> exception is supplied, folds in its throw site and cause.</summary>
    public static AppError Unexpected(
        string message = "An unexpected error occurred.",
        Exception? inner = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (inner is not null)
        {
            return AppError.FromException(AppErrorType.Unexpected, message, inner);
        }

        return AppError.Of(AppErrorType.Unexpected, message, null, member, file, line);
    }

    // Forbidden, Unauthorized, Conflict, DbTimeout, … follow the same shape.
}
```

**`src/Foundation/Validation/FieldError.cs`** — per-field failure (backlog: maybe `ValidationFailure`):
```csharp
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents a single field-level validation failure.</summary>
public sealed record FieldError
{
    /// <summary>Gets the member path that failed.</summary>
    public required string Property { get; init; }

    /// <summary>Gets the human-readable failure message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the stable rule code (from the validator).</summary>
    public required string Code { get; init; }
}
```

**`src/Foundation/Validation/ValidationError.cs`** — the canonical `AppError` subtype-for-members:
```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents an aggregate validation failure carrying its per-field failures.</summary>
public sealed record ValidationError : AppError
{
    /// <summary>Gets the per-field failures.</summary>
    public required IReadOnlyList<FieldError> Failures { get; init; }

    /// <summary>Creates a validation error from <paramref name="failures"/>.</summary>
    /// <param name="failures">The per-field failures.</param>
    public static ValidationError From(IReadOnlyList<FieldError> failures)
    {
        return new ValidationError
        {
            Type = AppErrorType.Validation,
            Message = "One or more validation errors occurred.",
            Failures = failures,
        };
    }
}
```

**`src/Foundation/Validation/ValidationException.cs`** — `.Error` *is* a `ValidationError` (no duplicate list):
```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents the thrown form of a <see cref="ValidationError"/>.</summary>
public sealed class ValidationException : AppException
{
    /// <summary>Initializes a new instance carrying <paramref name="error"/>.</summary>
    /// <param name="error">The aggregate validation error.</param>
    public ValidationException(ValidationError error)
        : base(error)
    {
    }
}
```

---

### 3.2 `Result` / `Result<T>` — the everywhere carrier

Lightweight closed DU for all **non-mediator** code; carries the same `AppError`; no context channels. Non-null + side-ownership come from the closed nested-case DU + `where T : notnull`.

**`src/Foundation/Results/ResultOfT.cs`**
```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Represents the outcome of an operation that returns a <typeparamref name="T"/> value.</summary>
/// <typeparam name="T">The success value type.</typeparam>
public abstract record Result<T> where T : notnull
{
    private Result()
    {
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>Represents a successful operation carrying its value.</summary>
    public sealed record Success : Result<T>
    {
        /// <summary>Gets the value produced by the operation.</summary>
        public required T Value { get; init; }
    }

    /// <summary>Represents a failed operation carrying its error.</summary>
    public sealed record Failure : Result<T>
    {
        /// <summary>Gets the error describing the failure.</summary>
        public required AppError Error { get; init; }
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The value to return.</param>
    public static Result<T> Ok(T value)
    {
        return new Success { Value = value };
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    public static Result<T> Fail(AppError error)
    {
        return new Failure { Error = error };
    }

    /// <summary>Collapses the result by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The type both handlers project to.</typeparam>
    /// <param name="onSuccess">Invoked with the value on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success success => onSuccess(success.Value),
            Failure failure => onFailure(failure.Error),
            _ => throw new InvalidOperationException("Result<T> has only Success and Failure cases."),
        };
    }
}
```

> Non-generic `Result` (in `Result.cs`) mirrors this without `Value` — `Ok()` / `Fail(error)` / `Match(Func<TOut> onSuccess, Func<AppError,TOut> onFailure)`.

---

### 3.3 `AppResult<TSuccess>` — mediator ↔ controllers

Collapsed from `<TSuccess,TFailure>` (failure is always `AppError`); pattern matching preserved; markers `ISuccessResult`/`IFailureResult` dropped; context markers kept. Deletes per-product `I{App}Failure` + per-op `{Op}Result.Failure`.

**`src/Mediator/Result/AppResult.cs`**
```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>Represents the outcome of an application operation — a typed success or an <see cref="AppError"/> failure, each with optional context.</summary>
/// <typeparam name="TSuccess">The success payload type.</typeparam>
public abstract record AppResult<TSuccess> where TSuccess : notnull
{
    private AppResult()
    {
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>Represents a successful operation carrying its data and optional context.</summary>
    public sealed record Success : AppResult<TSuccess>
    {
        /// <summary>Gets the typed success payload.</summary>
        public required TSuccess Data { get; init; }

        /// <summary>Gets the optional success-side context.</summary>
        public IAppSuccessContext? Context { get; init; }
    }

    /// <summary>Represents a failed operation carrying its error and optional context.</summary>
    public sealed record Failure : AppResult<TSuccess>
    {
        /// <summary>Gets the error describing the failure.</summary>
        public required AppError Error { get; init; }

        /// <summary>Gets the optional failure-side context.</summary>
        public IAppFailureContext? Context { get; init; }
    }

    /// <summary>Collapses the union by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The unified return type.</typeparam>
    /// <param name="onSuccess">Invoked with the success case.</param>
    /// <param name="onFailure">Invoked with the failure case.</param>
    public TOut Match<TOut>(Func<Success, TOut> onSuccess, Func<Failure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success success => onSuccess(success),
            Failure failure => onFailure(failure),
            _ => throw new InvalidOperationException("AppResult is a closed union."),
        };
    }
}
```

> ⚠️ `result-pattern.md` shows the **positional** `Success(Data, Context)` shape — it must be updated to this body-property form on the convention rewrite.

---

### 3.4 Bridge — return ⇄ throw (switch at any point)

| Direction | API |
|---|---|
| `AppError` → throw | `error.Throw([inner])` · `error.ToException([inner])` (on `AppError`, §3.1) |
| `Result<T>` → value/throw | `result.ValueOrThrow()` |
| `Result` → throw if failed | `result.ThrowIfFailure()` |
| invoke → `Result` (catch-all) | `(() => op()).Attempt()` · `(…).AttemptAsync()` |

`Attempt`/`AttemptAsync` (renamed from `Backbone…Catching.GetValue`): run a delegate, capture its outcome as our `Result<T>`. Using them **is** opting into catch-all (boundary tool — don't blanket the app). Retire `FuncResult<T>`. Async result = `Task<Result<T>>`/`ValueTask<…>` (no `AsyncResult`). Retry stays on Polly. Railway `Map`/`Bind`/`Tap` (+`*Async`) = LATER.

**`src/Foundation/Results/ResultExtensions.cs`**
```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Provides bridge extensions converting a <see cref="Result{T}"/> to the throw path.</summary>
public static class ResultExtensions
{
    /// <summary>Returns the success value, or throws the failure as an <see cref="AppException"/>.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result to unwrap.</param>
    public static T ValueOrThrow<T>(this Result<T> result) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            Result<T>.Success success => success.Value,
            Result<T>.Failure failure => throw failure.Error.ToException(),
            _ => throw new InvalidOperationException("Result<T> has only Success and Failure cases."),
        };
    }
}
```

**`src/Foundation/Results/AttemptExtensions.cs`**
```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Provides extensions that invoke a delegate and capture its outcome as a <see cref="Result{T}"/>.</summary>
public static class AttemptExtensions
{
    /// <summary>Invokes <paramref name="operation"/> and captures success or failure into a result.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="operation">The operation to invoke.</param>
    public static Result<T> Attempt<T>(this Func<T> operation) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            return Result<T>.Ok(operation());
        }
        catch (AppException appException)
        {
            return Result<T>.Fail(appException.Error);
        }
        catch (Exception exception)
        {
            return Result<T>.Fail(AppErrors.Unexpected(inner: exception));
        }
    }

    /// <summary>Invokes the asynchronous <paramref name="operation"/> and captures its outcome.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="operation">The asynchronous operation to invoke.</param>
    public static async ValueTask<Result<T>> AttemptAsync<T>(this Func<Task<T>> operation) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            var value = await operation().ConfigureAwait(false);

            return Result<T>.Ok(value);
        }
        catch (AppException appException)
        {
            return Result<T>.Fail(appException.Error);
        }
        catch (Exception exception)
        {
            return Result<T>.Fail(AppErrors.Unexpected(inner: exception));
        }
    }
}
```

---

### 3.5 Validation

FluentValidation stays behind the SDK `IValidator<T>` (swappable) + `FluentValidationAdapter<T>` + `AddFluentValidatorsFromAssemblies`. The standalone `ValidationResult` DU is **retired** — `Validate` returns `ValidationError?` (null = valid). `Guard.Against` stays `ArgumentException`-family.

**`src/Foundation/Validation/IValidator.cs`**
```csharp
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Defines the contract for validating an instance of <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Validates <paramref name="instance"/>, returning the aggregate error or <c>null</c> when valid.</summary>
    /// <param name="instance">The instance to validate.</param>
    ValidationError? Validate(T instance);

    /// <summary>Validates <paramref name="instance"/> and throws <see cref="ValidationException"/> when invalid.</summary>
    /// <param name="instance">The instance to validate.</param>
    void ValidateAndThrow(T instance);
}
```

The mediator `ValidationBehavior` calls `ValidateAndThrow` (unchanged shape); a handler that composes inline instead does `var error = validator.Validate(req); if (error is not null) { return AppResult<…>.Fail(error); }`. A consumer validator stays plain FluentValidation (`AbstractValidator<CreateUserRequest>`).

---

### 3.6 Mapping — `IErrorHttpStatusCodeMapper` (DI seam, edge-only)

Output-specific name (sibling `IErrorGrpcStatusCodeMapper` later). Lives in `Web` so Foundation stays transport-free. SDK default + app DI override. Kills per-product `ApiResults.ToStatusCode`. Returns **`int`** (not `HttpStatusCode`): matches every ASP.NET surface (`Response.StatusCode`, `ProblemDetails.Status`) cast-free, and admits non-standard codes the BCL enum can't represent.

**`src/Web/ErrorMapping/IErrorHttpStatusCodeMapper.cs`**
```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Defines the contract for mapping an <see cref="AppError"/> to an HTTP status code.</summary>
public interface IErrorHttpStatusCodeMapper
{
    /// <summary>Maps <paramref name="error"/> to an HTTP status code.</summary>
    /// <param name="error">The error to map.</param>
    int ToStatusCode(AppError error);
}
```

**`src/Web/ErrorMapping/DefaultErrorHttpStatusCodeMapper.cs`**
```csharp
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Provides the default <see cref="AppErrorType"/>-to-HTTP-status mapping; apps register their own to override.</summary>
public sealed class DefaultErrorHttpStatusCodeMapper : IErrorHttpStatusCodeMapper
{
    /// <inheritdoc/>
    public int ToStatusCode(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Type switch
        {
            AppErrorType.Validation => StatusCodes.Status400BadRequest,
            AppErrorType.Unauthorized or AppErrorType.ExternalUnauthorized => StatusCodes.Status401Unauthorized,
            AppErrorType.Forbidden => StatusCodes.Status403Forbidden,
            AppErrorType.NotFound or AppErrorType.FileNotFound => StatusCodes.Status404NotFound,
            AppErrorType.Conflict => StatusCodes.Status409Conflict,
            AppErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            AppErrorType.ExternalUnavailable => StatusCodes.Status503ServiceUnavailable,
            AppErrorType.DbTimeout or AppErrorType.OperationTimeout => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
```

---

### 3.7 ProblemDetails + global handlers

Keep `AddTraceAwareProblemDetails` (`traceId`/`requestId`). Handler chain: `ValidationExceptionHandler` (400 + `errors[]` + `code`) → `AppExceptionHandler` (any `AppException`) → fallback (non-`AppException` → 500, no leak). Auth: `AuthorizationBehavior` throws `AppException(AppErrors.Forbidden/Unauthorized)` → mapped (no more 500). `Origin` is **never** emitted.

**`src/Web/ExceptionHandling/AppExceptionHandler.cs`**
```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Maps a thrown <see cref="AppException"/> to an RFC 9457 ProblemDetails response.</summary>
public sealed class AppExceptionHandler(
    IErrorHttpStatusCodeMapper statusMapper,
    IErrorMessageResolver messageResolver,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            return false;
        }

        var error = appException.Error;
        var status = statusMapper.ToStatusCode(error);

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Type = $"urn:wow-two:error:{error.Type}",
            Status = status,
            Detail = messageResolver.Resolve(error, httpContext),
            Extensions = { ["code"] = error.Type.ToString() },
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = appException,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }
}
```

**Wire shape:**
```jsonc
{ "type":"urn:wow-two:error:NotFound", "title":"Not Found", "status":404,
  "detail":"Order 3f2… not found.", "code":"NotFound",
  "traceId":"…", "requestId":"…",
  "errors":[{ "property":"email","code":"NotEmptyValidator","message":"Email is required." }] }
```

> **One PD factory, two callers.** A shared `AppError.ToProblemDetails(httpContext, statusMapper, messageResolver)` is used by **both** the controller failure-arm (`.Match`) and the global handler — identical output. It: sets status/`code`/`type`/`detail`; emits `errors[]` when the error is a `ValidationError`/`AggregateError`; **promotes reserved `Metadata` keys to response headers** (`retryAfter`→`Retry-After`, `wwwAuthenticate`→`WWW-Authenticate`); never emits `Origin`. `ValidationExceptionHandler` becomes the **outside-mediator fallback** (mediator path converts validation throws → `Failure`, §3.9).
> **Framework errors** (route 404/405/415, model-binding 400) have no `AppError` — `CustomizeProblemDetails` **backfills `code` from the status** (404→`NotFound`) so the wire shape stays uniform; binding-400 already carries `errors`. They simply lack `Origin`/`Metadata`.

---

### 3.8 Infra failure classification & policy (external vs internal · retry/fallback)

Generalizes Haven's proven AI model (`AiProviderErrorType` + `AiProviderResponse`/`AiCallAttempt` + `AiFailoverClient`) — but **no new result type**: infra returns `Result<T>` carrying a *classified* `AppError`. External/internal/transient is **derived from `AppErrorType`**, not a parallel enum. Two pieces: **classify** (exception → type) and **policy** (type → disposition).

**`src/Foundation/Errors/ErrorPolicy.cs`**
```csharp
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents how a failure kind should be treated — origin, retryability, and fallback.</summary>
public sealed record ErrorDisposition
{
    /// <summary>Gets a value indicating whether the failure originates outside our code (dependency/network), not an internal bug.</summary>
    public required bool IsExternal { get; init; }

    /// <summary>Gets a value indicating whether the failure may resolve on retry.</summary>
    public required bool IsTransient { get; init; }

    /// <summary>Gets a value indicating whether a fallback path is worth attempting.</summary>
    public required bool AllowsFallback { get; init; }
}

/// <summary>Provides the disposition for each <see cref="AppErrorType"/>; the retry/fallback/log-level source of truth.</summary>
public static class ErrorPolicy
{
    private static readonly ErrorDisposition Transient = new() { IsExternal = true, IsTransient = true, AllowsFallback = true };
    private static readonly ErrorDisposition ExternalPermanent = new() { IsExternal = true, IsTransient = false, AllowsFallback = false };
    private static readonly ErrorDisposition Internal = new() { IsExternal = false, IsTransient = false, AllowsFallback = false };

    /// <summary>Gets the disposition for <paramref name="type"/>.</summary>
    /// <param name="type">The failure kind.</param>
    public static ErrorDisposition For(AppErrorType type)
    {
        return type switch
        {
            AppErrorType.DbTimeout or AppErrorType.OperationTimeout
                or AppErrorType.ExternalUnavailable or AppErrorType.TooManyRequests => Transient,
            AppErrorType.ExternalUnauthorized => ExternalPermanent,
            _ => Internal,
        };
    }
}

/// <summary>Defines the contract for classifying a caught exception into an <see cref="AppErrorType"/>.</summary>
public interface IExceptionClassifier
{
    /// <summary>Classifies <paramref name="exception"/>; returns <c>null</c> when not recognized.</summary>
    /// <param name="exception">The caught exception.</param>
    AppErrorType? Classify(Exception exception);
}
```

**`src/Data/Errors/NpgsqlExceptionClassifier.cs`** — source-specific classifier (lives in `Data` because it references Npgsql/EF; apps/SDK compose classifiers):
```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Data.Errors;

/// <summary>Classifies Npgsql and EF Core exceptions into <see cref="AppErrorType"/>.</summary>
public sealed class NpgsqlExceptionClassifier : IExceptionClassifier
{
    /// <inheritdoc/>
    public AppErrorType? Classify(Exception exception)
    {
        return exception switch
        {
            TimeoutException => AppErrorType.DbTimeout,
            DbUpdateConcurrencyException => AppErrorType.Conflict,
            PostgresException { SqlState: "23505" } => AppErrorType.Conflict,                  // unique violation
            PostgresException { SqlState: "57P03" or "53300" } => AppErrorType.ExternalUnavailable, // shutting down / too many connections
            NpgsqlException => AppErrorType.ExternalUnavailable,                               // connection-level
            _ => null,
        };
    }
}
```

`Attempt` gains a classifier overload; **both overloads rethrow `OperationCanceledException`** (cancellation is never a failure). Wire `Message` is a safe per-type default (`ErrorMessages.For(type)`), never `ex.Message` (which rides `causeChain`, log-only):
```csharp
/// <summary>Invokes <paramref name="operation"/>, classifying any thrown exception via <paramref name="classifier"/>.</summary>
public static Result<T> Attempt<T>(this Func<T> operation, IExceptionClassifier classifier) where T : notnull
{
    ArgumentNullException.ThrowIfNull(operation);
    ArgumentNullException.ThrowIfNull(classifier);

    try
    {
        return Result<T>.Ok(operation());
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (AppException appException)
    {
        return Result<T>.Fail(appException.Error);
    }
    catch (Exception exception)
    {
        var type = classifier.Classify(exception) ?? AppErrorType.Unexpected;

        return Result<T>.Fail(AppError.FromException(type, ErrorMessages.For(type), exception));
    }
}
```

> Retry/fallback stay on **Polly** (`AddStandardResilienceHandler`) — its predicate consults `ErrorPolicy`/the classifier (`IsTransient` → retry; `AllowsFallback` → failover, à la `AiFailoverClient`).

---

### 3.9 Mediator integration — never throw (terminal behavior)

The mediator **always returns `AppResult`** for `AppResult`-returning requests; an exception never escapes `Send`. A terminal `ExceptionToResultBehavior` (registered outermost) converts throws → `Failure`; only **non-mediator** throws reach the ASP.NET global handler.

**`src/Mediator/ExceptionHandling/ExceptionToResultBehavior.cs`**
```csharp
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.ExceptionHandling;

/// <summary>Converts an exception escaping a handler into an <c>AppResult.Failure</c> so the mediator never throws for result-returning requests.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type (an <c>AppResult&lt;TSuccess&gt;</c> for the conversion to apply).</typeparam>
public sealed class ExceptionToResultBehavior<TRequest, TResponse>(
    IExceptionClassifier classifier,
    AppErrorObserver observer) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AppException appException)
        {
            if (AppResultFactory.TryCreateFailure<TResponse>(appException.Error, out var failure))
            {
                return failure;
            }

            throw;
        }
        catch (Exception exception)
        {
            var type = classifier.Classify(exception) ?? AppErrorType.Unexpected;
            var error = AppError.FromException(type, ErrorMessages.For(type), exception);

            observer.Record(error, exception);

            if (AppResultFactory.TryCreateFailure<TResponse>(error, out var failure))
            {
                return failure;
            }

            throw;
        }
    }
}
```

- `AppResultFactory.TryCreateFailure<TResponse>` = cached reflection: if `TResponse` is `AppResult<TSuccess>`, builds its `Failure`; else `false` → the original exception rethrows to the global handler.
- **Validation** flows through: `ValidationBehavior` throws `ValidationException` → caught here → `Failure(ValidationError)`. Controllers always `.Match`; the failure-arm renders ProblemDetails via the shared factory (§3.7), emitting `errors[]`.

---

### 3.10 Logging & observability

Logging abstraction is already correct — consumers use `ILogger<T>`, Serilog is the provider (`UseSerilogConventional` + enrichers). **No abstraction change.** Add error logging + metrics + span status, emitted **once** at the seam (terminal behavior + global handler) via a shared `AppErrorObserver`.

**`src/Observability/Errors/AppErrorObserver.cs`**
```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Errors;

/// <summary>Records an <see cref="AppError"/> to logs, metrics, and the active trace span — the single error-observability seam.</summary>
public sealed partial class AppErrorObserver(ILogger<AppErrorObserver> logger)
{
    private static readonly Meter Meter = new("WoW.Two.Sdk.Errors");
    private static readonly Counter<long> ErrorsTotal = Meter.CreateCounter<long>("errors_total");

    /// <summary>Records <paramref name="error"/> (and its optional <paramref name="exception"/>) across logs, metrics, and the span.</summary>
    /// <param name="error">The error to record.</param>
    /// <param name="exception">The originating exception, when present.</param>
    public void Record(AppError error, Exception? exception = null)
    {
        ErrorsTotal.Add(1, new KeyValuePair<string, object?>("type", error.Type.ToString()));

        var activity = Activity.Current;
        activity?.SetStatus(ActivityStatusCode.Error, error.Message);
        if (exception is not null)
        {
            activity?.AddException(exception);
        }

        if (ErrorPolicy.For(error.Type).IsExternal)
        {
            LogExternal(error.Type.ToString(), error.Message, exception);
        }
        else
        {
            LogInternal(error.Type.ToString(), error.Message, exception);
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "External failure {Type}: {Message}")]
    private partial void LogExternal(string type, string message, Exception? exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Internal failure {Type}: {Message}")]
    private partial void LogInternal(string type, string message, Exception? exception);
}
```

- **Level by policy** (external→`Warning`, internal/`Unexpected`→`Error`; `Validation`/`NotFound` are non-external but low-severity — a finer policy table can split them later).
- **Correlation:** push `code` + `traceId` (`Activity.Current?.TraceId`) into `LogContext` so every error log links to its ProblemDetails (today `traceId` is PD-only). Exporters (OTLP/Prometheus/Azure Monitor) already wired — `errors_total` flows out; feeds the report/priority parked idea.

---

### 3.11 i18n groundwork (deferred — hooks only)

`IErrorMessageResolver` is the single seam for description **and** translation (a separate "description mapper" is redundant). SDK default = passthrough to `Message`. Always emit `code` so a future `IStringLocalizer`-backed impl (keyed on `Type` + `Metadata` args) translates with no call-site change. Fine-grained messages later → add an optional `MessageKey`.

**`src/Foundation/Errors/IErrorMessageResolver.cs`**
```csharp
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the contract for resolving the display message of an <see cref="AppError"/> for the current request.</summary>
public interface IErrorMessageResolver
{
    /// <summary>Resolves the message for <paramref name="error"/>, defaulting to <see cref="AppError.Message"/>.</summary>
    /// <param name="error">The error to resolve.</param>
    /// <param name="context">The current request context (carries the culture).</param>
    string Resolve(AppError error, HttpContext context);
}
```

> ⚠️ Near-term translation is *kind-level* (abstract enum + per-call-site `Message`) until `MessageKey` lands. Non-blocking; the seam keeps it open. *(Resolver may move out of `Foundation` to a `Web`/i18n slice when filled — placement TBD.)*

---

### 3.12 Frontend display (later layer)

| Failure | Surface |
|---|---|
| Validation (400 + `errors[]`) | inline **per-field** (`setError`) |
| Domain on submit (409/422) | `Alert` at form top / modal status |
| Transient (network/503) | `Toaster` danger |
| NotFound on page load | dedicated empty/404 state |
| Unexpected (500/render) | `ErrorBoundary` → error page (NEW) |
| Auth (401) | redirect to login (interceptor) |

Extend `ApiError` to parse `code` + `errors[]`; standardize toast-for-errors; add an `ErrorBoundary`. No i18n now (label-map hook ready once `code` is on the wire).

---

### 3.13 Cross-cutting & gaps (status)

Most are now designed (refs); the rest are explicit defers.

| Gap | Status |
|---|---|
| Cancellation | ✅ §3.4 / §3.8 / §3.9 — `OperationCanceledException` rethrown everywhere |
| Infra → `AppError` translation | ✅ §3.8 — `IExceptionClassifier` + `NpgsqlExceptionClassifier` |
| Logging policy | ✅ §3.10 — `AppErrorObserver`, level by `ErrorPolicy` |
| Observability | ✅ §3.10 — `errors_total` Meter + span status / `AddException` |
| Response headers | ✅ §3.7 — reserved `Metadata` keys → headers in the PD factory |
| Serialization | ✅ §3.1 — `[JsonIgnore] Origin`; `Metadata` log-only by default |
| Mediator error behavior | ✅ §3.9 — `ExceptionToResultBehavior` (never throw) |
| Framework errors | ✅ §3.7 — `CustomizeProblemDetails` backfills `code` from status |
| Multi-error | ✅ pattern (§3.1) — `AggregateError : AppError`, carrier stays single (`D-multi-error`) |
| Equality | ◻ open `D-equality` — rec skip custom `==` + `error.Is(AppErrorType)` helper (record `==` is a footgun) |
| Result combinators | ⏳ LATER — minimal `Map`/`Bind`/`Tap`/`Ensure` when a handler needs it |
| Success-side context | ⏳ hook only (§3.3) — apps define; spec when used |

---

## 4. Naming (final)

| was | now |
|---|---|
| `DomainError` | `AppError` |
| `DomainErrorCategory` (HTTP int) | `AppErrorType` (semantic, no int) |
| `DomainError.StatusCode` | *removed* → `IErrorHttpStatusCodeMapper` |
| per-product `FailureCategory` / `I{App}Failure` | *removed* → `AppError` + shared `AppErrorType` |
| thrown wrapper | `AppException` |
| `AppResult<TSuccess,TFailure>` | `AppResult<TSuccess>` (failure = `AppError`) |
| `ValidationError(Property,Message,Code)` | `FieldError` (backlog → `ValidationFailure`) |
| — (aggregate) | `ValidationError : AppError { FieldError[] Failures }` |
| `Backbone…GetValue/GetValueAsync` | `Attempt` / `AttemptAsync` |
| `AppError.Description` | `AppError.Message` |
| `ISuccessResult`/`IFailureResult` | *removed* |
| `IApplicationSuccessContext`/`IApplicationFailureContext` | `IAppSuccessContext` / `IAppFailureContext` |
| `FuncResultExtensions` (class) | `AttemptExtensions` |

---

## 5. Component inventory + when

| Component | Today | Action | When |
|---|---|---|---|
| `AppError` + `AppErrorType` + base catalog | `DomainError`(+HTTP int) / product `FailureCategory` | rename, reshape, unify | NOW |
| `AppException` (+ inner) | ad-hoc leaf exceptions | new single carrier | NOW |
| `ValidationError:AppError` + `FieldError` + `ValidationException` | `ValidationError`+`ValidationException` | reshape | NOW |
| `Result`/`Result<T>` → `AppError`, `notnull` | carries `DomainError` | re-point | NOW |
| `AppResult<TSuccess>` (drop `TFailure`/markers) | `<TSuccess,TFailure>`+markers | collapse | NOW |
| Bridge (`Throw`/`ValueOrThrow`/`ThrowIfFailure`/`Attempt[Async]`) | ❌ | new | NOW |
| `IErrorHttpStatusCodeMapper` + default (DRY-lift) | per-product `ApiResults` | new SDK seam | NOW |
| validation ProblemDetails emits `code`+`errors[]` | drops `code` | fix | NOW |
| `AppExceptionHandler` + fallback-500 | only validation mapped | new | NEXT |
| auth → 403 (throw `AppException`) | 500 bug | fix | NEXT |
| app error catalogs (`OrderErrors.*`) | inline `new(...)` | convention + ref impl (drydock) | NEXT |
| FE `code`/`errors[]` parse + per-field map | ❌ | new | NEXT |
| `IExceptionClassifier` (+ `NpgsqlExceptionClassifier`) · `ErrorPolicy`/`ErrorDisposition` | raw infra throws | new (classify + policy) | NEXT |
| `ExceptionToResultBehavior` (mediator never-throw) | only validation throws | new | NEXT |
| `AppErrorObserver` (`errors_total` Meter + span status + level-by-policy log) | none | new | NEXT |
| shared `AppError.ToProblemDetails` factory (+ headers + framework backfill) | scattered | new | NEXT |
| `error.Is(AppErrorType)` helper | ❌ | new (small) | NOW |
| conventions (3 docs) rewrite | contradictory | merge to final model | NEXT |
| **Deletes** | `DomainError`, `ISuccessResult`/`IFailureResult`, `ValidationResult` | remove | NEXT |
| **Consumer migration** (apps) | per-app `FailureCategory`/`I{App}Failure`/`ApiResults` | delete; controllers use SDK mapper; handlers → `AppResult<TSuccess>`+`AppError` | NEXT |
| `IErrorMessageResolver` + FE label-map (i18n) | ❌ | hook NOW, fill | LATER |
| FE toast-for-errors std + `ErrorBoundary`/page | ad-hoc | standardize | LATER |
| `MessageKey` (fine-grained i18n) | ❌ | add when needed | LATER |
| `AggregateError : AppError` (multi-error subtype) | single only | new when needed | LATER |
| error-code analyzer (format/uniqueness) | ❌ | new | LATER |
| `FieldError` → `ValidationFailure` rename | `FieldError` | rename | LATER |

---

## 6. Open decisions

- **D-validation-result:** retire the standalone `ValidationResult` DU (fold into `Result`/`ValidationError?`) — *rec: yes*.
- **D-multi-error (resolved):** carrier stays single `AppError`; multiplicity via an `AggregateError : AppError { IReadOnlyList<AppError> Errors }` subtype (mirrors `ValidationError`), added when first needed.
- **D-cancellation (resolved):** `OperationCanceledException` is rethrown by `Attempt`, the mediator behavior, and the global handler — never wrapped.
- **D-mediator (resolved):** the mediator never throws for `AppResult` requests — `ExceptionToResultBehavior` converts throws → `Failure`; only non-mediator throws reach the ASP.NET global handler (§3.9).
- **D-classify-policy (resolved):** one `AppErrorType` + `IExceptionClassifier` (exception→type) + `ErrorPolicy` (type→disposition); external/transient is derived, not a parallel enum (§3.8).
- **D-equality (open):** skip custom `AppError` equality + add `error.Is(AppErrorType)`, vs override `==` to `Type`-only — *rec: skip + helper*.
- **D-type-uri:** ProblemDetails `type` = opaque URN (`urn:wow-two:error:<type>`) vs docs URL — *rec: URN now, URL when docs exist*.
- **D-base-enum-extent:** confirm the `AppErrorType` value set in §3.1 (esp. which technical kinds ship in v1).
- **D-conventions-distill:** on approval, rewrite `result-pattern.md` + `validation.md` + `problem-details.md` to this model + add a `targets.md` entry.
- **D-error-exception-model (resolved):** `AppException` *carries* `AppError` (one source of truth); `AppError` is a **pure value** (no live `Exception`) — caught exceptions fold in via `AppError.FromException` (`Origin` from stack + cause type-name) and ride native `AppException.InnerException` on throw. Rejected full symmetry (duplication / drift).
- **D-mapper-return (resolved):** `ToStatusCode` returns `int` (cast-free with ASP.NET surfaces; admits non-standard codes), not `HttpStatusCode`.

---

## 7. Parked ideas (later brainstorm)

- **`HelpLink`** on `AppError` (reuse `Exception.HelpLink` on throw) → docs page for complex errors (what/why/what-to-do); maps to ProblemDetails `type` or a `helpLink` extension.
- **Remediation / guidance** — optional "what to do" (`"fix a,b,c"` / `"retry"`), distinct from `Message`; shown beside it (forms: field errors + a guidance line).
- **User report / flag-for-priority** — "report this error" tying an occurrence (`Type`+`traceId`+`Origin`) to an ops dashboard that bumps priority — likely lands in `drydock` (ops plane).

---

## 8. Sources

MS: design-guidelines/exceptions · exceptions/best-practices · aspnet/core error-handling-api · aspnet/core localization. Repos/specs: github.com/error-or/error-or · github.com/altmann/FluentResults · rfc-editor.org/rfc/rfc9457. Blogs: milanjovanovic.tech (result pattern, ProblemDetails, global handling, CQRS+FluentValidation) · andrewlock.net (result pattern) · swagger.io (RFC 9457). Ardalis.Result / CSharpFunctionalExtensions / language-ext from knowledge. *(Harness verify was rate-limited; validated by hand against the above.)*
