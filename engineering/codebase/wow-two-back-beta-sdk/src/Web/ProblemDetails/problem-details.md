# ProblemDetails

*Last updated: 2026-10-02*

> RFC 9457 HTTP errors and trace context from `WoW2.Sdk.Backend.Beta`.

```csharp
using WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;
builder.Services.AddHttpProblemDetails();
var app = builder.Build();
app.UseHttpProblemDetails();
```

`AddTraceAwareProblemDetails()` remains the lower-level registration.
The complete opt-in HTTP boundary adds the SDK exception chain, status pages and MVC result normalization.

[API](HttpProblemDetails.spec.md) · [Contract](HttpProblemDetails.standard.md) ·
[ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling) ·
[RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)
