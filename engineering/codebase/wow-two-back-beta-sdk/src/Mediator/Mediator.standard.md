# Mediator — standard

*Last updated: 2026-05-04*

> Behavioral contract for the in-process mediator. RFC 2119.

## Purpose

Provide a small, MediatR-API-compatible mediator so consumers don't take a hard dependency on MediatR (which moved to a commercial license in 2025).

## Behavior

### Surface

- `IRequest<TResponse>` and `IRequest` (= `IRequest<Unit>`) **MUST** be the only request markers.
- `INotification` **MUST** be the notification marker.
- `IRequestHandler<TRequest, TResponse>` and `IRequestHandler<TRequest>` (= `IRequestHandler<TRequest, Unit>`) **MUST** be the handler interfaces.
- `INotificationHandler<TNotification>` **MUST** be the notification handler.
- `IPipelineBehavior<TRequest, TResponse>` **MUST** be the cross-cut for request flows.
- `ISender`, `IPublisher`, and `IMediator` (= both) **MUST** be the public surface for invoking the pipeline.

### Send

- `Send<TResponse>(IRequest<TResponse>)` **MUST** resolve exactly one `IRequestHandler<TRequest, TResponse>` from DI.
- All registered `IPipelineBehavior<TRequest, TResponse>` **MUST** be invoked in registration order surrounding the handler.
- `Send(IRequest)` (no response) **MUST** dispatch as `Send<Unit>(...)`.
- A request whose handler is missing **MUST** throw `InvalidOperationException` (via `GetRequiredService`).

### Publish

- `Publish<TNotification>(...)` **MUST** invoke every registered `INotificationHandler<TNotification>` sequentially in registration order.
- A failure in one handler **MUST** propagate immediately (no swallowing). For fan-out resilience, consumers wrap handlers themselves.

### Cancellation

- All operations **MUST** propagate the supplied `CancellationToken`.

### Performance

- The mediator **SHOULD** cache the per-request-type dispatcher delegate for hot-path perf.
- Reflection **MAY** be used to build dispatchers on first dispatch but **MUST NOT** be on the hot path after warm-up.

### Registration

- `AddMediator(Assembly[])` **MUST** scan supplied assemblies for closed `IRequestHandler<,>` / `INotificationHandler<>` implementations and register them as `Transient`.
- `IMediator` **MUST** be registered as `Transient` and reused as `ISender` / `IPublisher`.
- `AddMediatorBehavior(typeof(B<,>))` **MUST** register an open-generic behavior with `Transient` lifetime.

### Failure modes

- Behaviors **MUST** propagate exceptions thrown by inner behaviors / handlers.
- A behavior **MAY** short-circuit by not calling `next()`.
- A behavior **MUST NOT** call `next()` more than once.

## Non-goals

- Distributed messaging — see `Messaging.*` packages.
- Streaming requests (`IStreamRequest<T>` in MediatR v10+). Add later if demand arises.
- Pre/post processors as separate concepts. Behaviors cover the same surface.

## See also

- [Mediator.spec.md](./Mediator.spec.md)
