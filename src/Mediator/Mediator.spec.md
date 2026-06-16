# Mediator — spec

*Last updated: 2026-06-14*

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
| `ISender.SendAsync<T>(IRequest<T>)` | Returns `ValueTask<T>`. |
| `ISender.SendAsync(IRequest)` | Returns `ValueTask<Unit>` (dispatches as `SendAsync<Unit>`). |
| `IPublisher.PublishAsync<T>(T)` | `ValueTask` — sequential fan-out. |
| `IMediator` | Combined. |

### Registration

| Method | Notes |
|---|---|
| `AddMediator()` | Scans calling assembly. |
| `AddMediator(params Assembly[])` | Scans supplied assemblies. |
| `AddMediatorBehavior(typeof(B<,>))` | Register open-generic behavior. |

### CQRS layer (`Cqrs/`)

Intent markers layered over the request/handler primitives — same dispatch, clearer naming. The DI scan already binds these (an `IQueryHandler<,>` **is** an `IRequestHandler<,>`); no scanner change.

| Type | Notes |
|---|---|
| `IQuery<TResult>` | `: IRequest<TResult>` — read marker. (Invariant `TResult` — the SDK's `IRequest<TResponse>` is invariant.) |
| `ICommand<TResult>` | `: IRequest<TResult>` — write marker (value). |
| `ICommand` | `: IRequest` — write marker (void). |
| `IQueryHandler<TQuery, TResult>` | `: IRequestHandler<TQuery, TResult> where TQuery : IQuery<TResult>`. |
| `ICommandHandler<TCommand, TResult>` | `: IRequestHandler<TCommand, TResult> where TCommand : ICommand<TResult>`. |
| `ICommandHandler<TCommand>` | `: IRequestHandler<TCommand> where TCommand : ICommand`. |

`SendAsync` is native on `ISender` — `sender.SendAsync(query)` / `sender.SendAsync(command)` bind to `ISender.SendAsync<T>(IRequest<T>)` directly (a query/command **is** an `IRequest<T>`). No separate CQRS sender facade.

### Result union (`Result/`)

A closed discriminated union every handler/endpoint returns — typed success **or** typed, context-bearing failure. See [`result-pattern.md`](../../../../conventions/development/backend/foundation/result-pattern.md).

| Type | Notes |
|---|---|
| `AppResult<TSuccess, TFailure>` | `abstract record`, private ctor; `where TSuccess : ISuccessResult, TFailure : IFailureResult`. |
| `AppResult<,>.Success` | `sealed record Success(TSuccess Data, IApplicationSuccessContext? Context = null)`. |
| `AppResult<,>.Failure` | `sealed record Failure(TFailure Error, IApplicationFailureContext? Context = null)`. |
| `ISuccessResult` / `IFailureResult` | Empty markers constraining the payloads. |
| `IApplicationSuccessContext` / `IApplicationFailureContext` | Empty markers — per-side optional context. |
| `AppResultExtensions.Match<…, TOut>(onSuccess, onFailure)` | Collapse to one `TOut`. **Two overloads** — `Func<Success, TOut>` (payload) and `Func<TOut>` (no-arg, for void-ish commands → `NoContent`); failure arm is always `Func<Failure, TOut>`. |

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
    public async ValueTask<UserDto> HandleAsync(GetUser request, CancellationToken ct) =>
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
    public async Task<UserDto> Get(Guid id, CancellationToken ct) =>
        await mediator.SendAsync(new GetUser(id), ct);
}
```

Notifications:

```csharp
public sealed record OrderPlaced(Guid OrderId) : INotification;

public class SendOrderEmail : INotificationHandler<OrderPlaced> { /* ... */ }
public class TrackAnalytics : INotificationHandler<OrderPlaced> { /* ... */ }

await mediator.PublishAsync(new OrderPlaced(orderId), ct);
```

Pipeline behaviors:

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> HandleAsync(TRequest req, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
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

## CQRS + AppResult

A query/command + handler returning the standard union, consumed via `.Match`:

```csharp
// per-operation result container — implements the markers
public abstract record CodeGetByIdResult
{
    private CodeGetByIdResult() { }
    public sealed record Success(CodeDto Code) : CodeGetByIdResult, ISuccessResult;
    public sealed record Failure(string ErrorMessage, bool NotFound = false) : CodeGetByIdResult, IFailureResult;
}

public sealed record CodeGetByIdQuery(Guid Id) : IQuery<AppResult<CodeGetByIdResult.Success, CodeGetByIdResult.Failure>>;

public sealed class CodeGetByIdHandler(MyDb db)
    : IQueryHandler<CodeGetByIdQuery, AppResult<CodeGetByIdResult.Success, CodeGetByIdResult.Failure>>
{
    public async ValueTask<AppResult<CodeGetByIdResult.Success, CodeGetByIdResult.Failure>> HandleAsync(CodeGetByIdQuery q, CancellationToken ct)
        => await db.Codes.FindAsync([q.Id], ct) is { } code
            ? new AppResult<CodeGetByIdResult.Success, CodeGetByIdResult.Failure>.Success(new(code.ToDto()))
            : new AppResult<CodeGetByIdResult.Success, CodeGetByIdResult.Failure>.Failure(new("Code not found", NotFound: true));
}

// controller — SendAsync facade + .Match collapse
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
    (await sender.SendAsync(new CodeGetByIdQuery(id), ct)).Match<IActionResult>(
        onSuccess: ok => Ok(ApiResponse<CodeDto>.Ok(ok.Data.Code)),
        onFailure: fail => fail.Error.NotFound ? NotFound() : Problem(fail.Error.ErrorMessage));

// void-ish command — no-arg success arm → NoContent
[HttpDelete("{id:guid}")]
public async Task<IActionResult> DeleteById(Guid id, CancellationToken ct) =>
    (await sender.SendAsync(new CodeDeleteCommand(id), ct)).Match<IActionResult>(
        onSuccess: () => NoContent(),
        onFailure: fail => Problem(fail.Error.ErrorMessage));
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
