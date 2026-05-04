# WoW.Two.Sdk.Backend.Beta.Web.RateLimit

> Conventional `Microsoft.AspNetCore.RateLimiting` policy — sliding window, per-IP, 100 req/min default.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.RateLimit
```

## Usage

```csharp
builder.Services.AddWowTwoRateLimit();

var app = builder.Build();
app.UseRateLimiter();

// Apply to all endpoints
app.MapGroup("/api").RequireRateLimiting(RateLimitServiceCollectionExtensions.DefaultPolicyName);
```
