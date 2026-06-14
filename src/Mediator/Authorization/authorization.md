# WoW.Two.Sdk.Backend.Beta.Mediator.Authorization

> Pipeline behavior — authorize requests marked with `IRequireAuthorization` via ASP.NET Core's `IAuthorizationService`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Mediator.Authorization
```

## Usage

```csharp
builder.Services.AddMediator(typeof(Program).Assembly);
builder.Services.AddMediatorAuthorizationBehavior();
```

Mark requests:

```csharp
public sealed record DeleteUser(Guid Id) : IRequest, IRequireAuthorization
{
    public string? PolicyName => "Admin";
}
```

Throws:
- `UnauthorizedAccessException` — caller not authenticated
- `AuthorizationException` — caller lacks permission

Map to RFC 7807 401/403 via [`Web.ProblemDetails`](../../web/problemdetails/problem-details.md).
