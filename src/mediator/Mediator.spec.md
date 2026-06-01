# Mediator — spec

*Last updated: 2026-05-04*

## NuGet

```
WoW.Two.Sdk.Backend.Beta.Mediator
```

## Public API

### Markers

| Type | Notes |
|---|---|
| `IRequest<TResponse>` | Request producing `TResponse`. |
| `IRequest` | = `IRequest<Unit>`. |
| `INotification` | Fan-out marker. |
| `Unit` | Void-equivalent struct. |

### Handlers

| Type | Notes |
|---|---|
| `IRequestHandler<TRequest, TResponse>` | One per request type. |
| `IRequestHandler<TRequest>` | = `IRequestHandler<TRequest, Unit>`. |
| `INotificationHandler<TNotification>` | Many per notification type. |

### Pipeline

| Type | Notes |
|---|---|
| `IPipelineBehavior<TRequest, TResponse>` | Wraps handler. Open-generic. |
| `RequestHandlerDelegate<TResponse>` | Continuation. |

### Surface

| Type | Notes |
|---|---|
| `ISender.Send<T>(IRequest<T>)` | Returns `Task<T>`. |
| `ISender.Send(IRequest)` | Returns `Task`. |
| `IPublisher.Publish<T>(T)` | Sequential fan-out. |
| `IMediator` | Combined. |

### Registration

| Method | Notes |
|---|---|
| `AddMediator()` | Scans calling assembly. |
| `AddMediator(params Assembly[])` | Scans supplied assemblies. |
| `AddMediatorBehavior(typeof(B<,>))` | Register open-generic behavior. |

## Quick start

```csharp
using WoW.Two.Sdk.Backend.Beta.Mediator;

builder.Services.AddMediator(typeof(Program).Assembly);
```

Define a request + handler:

```csharp
public sealed record GetUser(Guid Id) : IRequest<UserDto>;

public sealed class GetUserHandler(MyDb db) : IRequestHandler<GetUser, UserDto>
{
    public async Task<UserDto> Handle(GetUser request, CancellationToken ct) =>
        await db.Users.FindAsync([request.Id], ct) is { } user
            ? new UserDto(user.Id, user.Email)
            : throw new KeyNotFoundException();
}
```

Send:

```csharp
public class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<UserDto> Get(Guid id, CancellationToken ct) =>
        mediator.Send(new GetUser(id), ct);
}
```

Notifications:

```csharp
public sealed record OrderPlaced(Guid OrderId) : INotification;

public class SendOrderEmail : INotificationHandler<OrderPlaced> { /* ... */ }
public class TrackAnalytics : INotificationHandler<OrderPlaced> { /* ... */ }

await mediator.Publish(new OrderPlaced(orderId), ct);
```

Pipeline behaviors:

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest req, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        log.LogInformation("→ {Request}", typeof(TRequest).Name);
        var sw = Stopwatch.StartNew();
        var response = await next();
        log.LogInformation("← {Request} in {Elapsed}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return response;
    }
}

builder.Services.AddMediatorBehavior(typeof(LoggingBehavior<,>));
```

## Behavior order

Behaviors execute in registration order — first registered = outermost wrapper.

```
Behavior A (registered first)  [outer]
   Behavior B
      Behavior C                [innermost]
         Handler
```

## See also

- [Mediator.standard.md](./Mediator.standard.md)
- Pre-built behaviors: `Mediator.Validation`, `Mediator.Logging`, `Mediator.Authorization`, `Mediator.Idempotency`
