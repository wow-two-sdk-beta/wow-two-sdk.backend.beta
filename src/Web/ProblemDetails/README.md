# WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails

> RFC 7807 ProblemDetails — built-in `AddProblemDetails` + automatic `traceId` and `requestId` enrichment.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails
```

## Usage

```csharp
builder.Services.AddTraceAwareProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
```

Throw `Microsoft.AspNetCore.Http.BadHttpRequestException` etc. — they map to ProblemDetails automatically. Or return `DomainError`-shaped results from your handlers and convert via the [`Errors`](../../foundation/errors/README.md) package.

## See also

- [ASP.NET Core ProblemDetails](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)
- [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807)
