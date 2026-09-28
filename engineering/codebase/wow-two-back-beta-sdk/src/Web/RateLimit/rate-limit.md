# WoW.Two.Sdk.Backend.Beta.Web.RateLimit

> Conventional `Microsoft.AspNetCore.RateLimiting` policy — sliding window, per-IP, 100 req/min default.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.RateLimit
```

## Usage

```csharp
builder.Services.AddPerIpSlidingWindowRateLimit();

var app = builder.Build();
app.UseRateLimiter();

// Apply to all endpoints
app.MapGroup("/api").RequireRateLimiting(RateLimitServiceCollectionExtensions.DefaultPolicyName);
```

## Policies from configuration

`AddApiDefaults` also adds every policy declared under `RateLimits` — no code change beyond `RequireRateLimiting(name)`:

```jsonc
"RateLimits": {
  "Policies": {
    "login": { "PermitLimit": 5, "Window": "00:01:00", "Algorithm": "FixedWindow", "PartitionBy": "Ip" },
    "api":   { "PermitLimit": 600, "Window": "00:01:00", "PartitionBy": "User" }
  },
  "GlobalPolicy": "api"                   // optional: applies to every request
}
```

- Algorithms: `SlidingWindow` (default), `FixedWindow`, `TokenBucket`, `Concurrency`; partitions: `Ip`, `User`, `Tenant`
  (user and tenant fall back to the IP).
- Rejections answer `429` with `Retry-After` and a problem-details body (`code: TooManyRequests`), translated when
  error translation is on. A `GlobalPolicy` naming no policy fails when the host starts.
- Policies bind when the limiter is built, so configuration a test host adds after registration applies.
