# WoW.Two.Sdk.Backend.Beta.Errors

> Canonical `DomainError` record + category-to-HTTP-status mapping. Thin building block consumed by the validation pipeline, mediator behaviors, and ProblemDetails mappers.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Errors
```

## Usage

```csharp
using WoW.Two.Sdk.Backend.Beta.Errors;

public ErrorOr<User> GetUser(Guid id) =>
    _repo.Find(id) is { } user
        ? user
        : DomainError.NotFound("user.not_found", "User not found");
```

`StatusCode` is derived from the `Category` enum value — no manual mapping needed.

## See also

- [WoW.Two.Sdk.Backend.Beta.Results](../results/README.md) for the result wrappers
- [WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails](../../web/problemdetails/README.md) (P1) for the HTTP mapper
