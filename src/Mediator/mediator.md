# WoW.Two.Sdk.Backend.Beta.Mediator

> In-process MediatR-API-compatible mediator. No MediatR dependency — Wow Two implementation under MIT.

## Why

MediatR moved to a commercial license in 2025. This package preserves the same surface (`IRequest<T>`, `IRequestHandler<,>`, `INotification`, `INotificationHandler<>`, `IPipelineBehavior<,>`, `IMediator`) so existing code migrates with a namespace swap.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Mediator
```

## Usage

```csharp
using WoW.Two.Sdk.Backend.Beta.Mediator;

builder.Services.AddMediator(typeof(Program).Assembly);

// register pipeline behaviors
builder.Services.AddMediatorBehavior(typeof(LoggingBehavior<,>));
```

### CQRS + result (`Cqrs/` · `Result/`)

Intent markers + the standard result union layered over the primitives:

```csharp
public sealed record GetCodeQuery(Guid Id) : IQuery<AppResult<GetCodeResult.Success, GetCodeResult.Failure>>;

// dispatch via the SendAsync facade, collapse via .Match
(await sender.SendAsync(new GetCodeQuery(id), ct)).Match<IActionResult>(
    onSuccess: ok => Ok(ok.Data),       // or () => NoContent() for void-ish commands
    onFailure: fail => Problem(fail.Error.ErrorMessage));
```

- `Cqrs/` — `IQuery<T>` / `ICommand<T>` / `ICommand` + `IQueryHandler` / `ICommandHandler` (markers over `IRequest`/`IRequestHandler`; same DI scan) + `SendAsync` on `ISender`.
- `Result/` — `AppResult<TSuccess, TFailure>` closed union (`Success`/`Failure` cases) + `ISuccessResult` / `IFailureResult` / context markers + `.Match`. See [`result-pattern.md`](../../../../conventions/development/backend/foundation/result-pattern.md).

See [Mediator.spec.md](./Mediator.spec.md) for the full surface and patterns.

## See also

- [Standard](./Mediator.standard.md) · [Spec](./Mediator.spec.md)
- Companion behaviors: `Mediator.Validation`, `Mediator.Logging`, `Mediator.Authorization`, `Mediator.Idempotency`
