# WoW.Two.Sdk.Backend.Beta.Foundation.Errors

> The transport-agnostic failure model: `AppError` (value) + `AppErrorType` (kind) + `AppException` (throw form), the `AppErrors` catalog, descriptive `ErrorNature`, and the extensible exception→`AppError` mapping seam. Consumed by results, the mediator behaviors, and the ProblemDetails handlers. Part of the core mono-lib (`WoW2.Sdk.Backend.Beta`) — no separate package.

## Model

- **`AppError`** — open `record` carrying `Type` (`AppErrorType`), `Message`, optional `Metadata`, and log-only `Origin`. Authored via `AppError.Of(...)`, `AppError.FromException(...)`, or the `AppErrors` catalog (`AppErrors.NotFound(...)`, `Conflict`, `Validation`, …). `Type` is the wire `code`.
- **`AppException`** — the throw form; *carries* an `AppError` (one source of truth). Bridge both ways: `error.Throw()` / `error.ToException()`.
- **`ErrorNature {Transient, Permanent, Defect}`** via `IErrorNatureClassifier` (DI) — consumers derive retry/fallback/log level.

## Returning vs throwing

```csharp
// return (expected failure) — see ../Results/results.md
public Result<User> GetUser(Guid id)
    => _repo.Find(id) is { } user ? user : AppErrors.NotFound("User not found");

// throw (exceptional) — the mediator behavior / global handler turns it into ProblemDetails
AppErrors.Conflict("Email already registered.").Throw();
```

## Mapping a caught exception → AppError

`IExceptionMapper.Map(Exception)` is the total facade the mediator behavior and global handlers use. The default `ExceptionMapper` unwraps an `AppException`, then walks the registered `IExceptionMappingRule` contributors **last-registered-first**, else falls back to `Unexpected`. The SDK ships `DbExceptionMappingRule` (Npgsql/EF → `AppError`), auto-wired by `AddPostgresPersistence`.

```csharp
// map an exception the SDK does not know — your rule shadows SDK rules for the same exception
public sealed class PaymentDeclinedRule : IExceptionMappingRule
{
    public AppError? TryMap(Exception ex)
        => ex is PaymentDeclinedException ? AppErrors.PaymentRequired("Card declined.") : null;
}

builder.Services.AddExceptionMappingRule<PaymentDeclinedRule>();
```

## See also

- [Foundation.Results](../Results/) — `Result` / `Result<T>` wrappers
- [Web.ErrorMapping / Web.ExceptionHandling](../../Web/ExceptionHandling/) — `IErrorHttpStatusCodeMapper`, the ProblemDetails factory + handlers
- [`docs/planning/errors/errors-architecture-investigation.md`](../../../docs/planning/errors/errors-architecture-investigation.md) — full design record
