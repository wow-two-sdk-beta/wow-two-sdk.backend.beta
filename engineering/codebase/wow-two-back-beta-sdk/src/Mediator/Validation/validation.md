# WoW.Two.Sdk.Backend.Beta.Mediator.Validation

> Request-validation interceptor — runs all registered SDK `IValidator<TRequest>` instances before the handler.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Mediator.Validation
```

## Usage

```csharp
builder.Services.AddFluentValidatorsFromAssemblies(typeof(Program).Assembly);
builder.Services.AddMediator(typeof(Program).Assembly);
builder.Services.AddMediatorValidatingInterceptor();
```

The interceptor runs every registered SDK validator, combines their field failures in registration order, and throws one `WoW.Two.Sdk.Backend.Beta.Foundation.Validation.ValidationException`. Register `AddValidationExceptionHandler` or `AddAppExceptionHandling` and [`Web.ProblemDetails`](../../Web/ProblemDetails/problem-details.md) to render the failure as an HTTP 400 ProblemDetails response.

## Phase placement

Use the interceptor for target-free requests whose authored field validation belongs before the handler.

For a command that addresses an existing target, resolve the target and check ownership first. Run the request's SDK validator from the handler or shared application service only after those checks. The generic interceptor cannot establish that phase order because it always runs before the handler.

Keep persistence and other I/O out of static payload validators. Transition rules that need prior state belong on the resolved entity or a domain validator.

## Contract boundaries

- `IValidator<T>.Validate` is synchronous and returns `null` when valid.
- `IValidator<T>.Inspect` reports advisory failures without rejecting a write.
- FluentValidation supplies the default field-rule adapter; callers depend on the SDK interface.
- Validate copied or deserialized candidates where the application accepts them for use or persistence.
- Async validation, caller-selected rulesets, and target-resolution integration require separate explicit seams.
