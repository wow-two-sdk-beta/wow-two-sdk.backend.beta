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

See [Mediator.spec.md](./Mediator.spec.md) for the full surface and patterns.

## See also

- [Standard](./Mediator.standard.md) · [Spec](./Mediator.spec.md)
- Companion behaviors: `Mediator.Validation`, `Mediator.Logging`, `Mediator.Authorization`, `Mediator.Idempotency`
