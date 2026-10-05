# HTTP ProblemDetails — spec

*Last updated: 2026-10-02*

> Reuses SDK error mapping with a complete opt-in HTTP boundary.

## Package

`WoW2.Sdk.Backend.Beta`

## Usage

```csharp
using WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

builder.Services.AddHttpProblemDetails();
builder.Services.AddControllers();
var app = builder.Build();
app.UseHttpProblemDetails();
// Product boundary middleware, authentication and authorization follow here.
app.MapControllers();
```

## Public API

- `AddHttpProblemDetails(IServiceCollection)` returns the same service collection.
- `UseHttpProblemDetails(IApplicationBuilder)` returns the same application builder.
- Registration installs trace enrichment, SDK exception handlers and MVC result normalization.
- Middleware handles exceptions plus empty error statuses without buffering successful content.
- MVC string/object error results become safe ProblemDetails; existing validation errors are retained.
- An implicit MVC result status falls back to the current HTTP response status.
- `CustomizeProblemDetails` runs once per problem, including problems already customized by MVC's factory.
- The writer retains the supplied `ProblemDetailsContext.Exception` and `AdditionalMetadata`.
- Product-authored middleware error bodies must already follow the standard.
- An opt-in JSON writer bypasses `Accept` negotiation, including MVC `ReturnHttpNotAcceptable=true`.
- Existing SDK exception handlers and `Results.Problem` share that writer.
- Known application exceptions use the SDK error mapper and ProblemDetails factory.
- `BadHttpRequestException` retains its HTTP status with safe request-error text.

## Adoption

The complete boundary remains opt-in. Enabling it replaces arbitrary MVC error payloads
with ProblemDetails, including errors whose status is assigned through `Response.StatusCode`.
Keep actionable client metadata in ProblemDetails extensions or mapped application errors.
Successful payloads and started responses keep their existing contracts. The lower-level
`AddTraceAwareProblemDetails()` adds fallback trace, type, title, instance and status-code metadata.

[Contract](HttpProblemDetails.standard.md).

The opt-in registration places the boundary before framework startup middleware.
Host filtering suppresses its HTML failure message so rejected hosts also receive
ProblemDetails. Repeated `UseHttpProblemDetails()` calls do not duplicate the pipeline.
