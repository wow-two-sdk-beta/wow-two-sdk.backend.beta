# WoW.Two.Sdk.Backend.Beta.Mediator.Validation

> FluentValidation pipeline behavior — runs all registered `IValidator<TRequest>` before the handler.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Mediator.Validation
```

## Usage

```csharp
builder.Services.AddFluentValidatorsFromAssemblies(typeof(Program).Assembly);   // FluentValidation registration
builder.Services.AddMediator(typeof(Program).Assembly);
builder.Services.AddMediatorValidationBehavior();                   // wires the behavior
```

Throws `FluentValidation.ValidationException` if any rule fails — pair with [`Web.ProblemDetails`](../../web/problemdetails/problem-details.md) to map to RFC 7807 400 responses.
