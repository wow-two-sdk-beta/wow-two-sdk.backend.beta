# Web.Quotas

Usage quotas for free tiers and plans: count units per subject in a UTC window, gate endpoints on them, and show the
standing. Rate limits bound bursts; quotas bound allowances ("3 conversions a day").

## Quick start

```csharp
builder.Services.AddQuotas();                                  // off until Quotas:Enabled
builder.Services.AddRedisQuotaRepository(redisConnection);     // shared counters across hosts (optional)

app.MapPost("/convert", …).RequireQuota("conversions");
app.MapGroup("/me").MapQuotaUsageEndpoint();                   // GET /me/quotas
```

```jsonc
"Quotas": { "Enabled": true, "Definitions": {
  "conversions": { "Period": "Day", "Limit": 3, "Plans": { "pro": 500, "team": -1 } }
} }
```

## Notes

- Windows: `Day`, `Week` (ISO), `Month`, `Year` in UTC, or `Total`; a negative limit is unlimited.
- Subject: the signed-in user (`sub`, then name identifier) with the `plan` claim, else the client address.
  Replace `IQuotaSubjectService` to read plans from the product's data.
- An exhausted quota answers `429` with `Retry-After` until the window ends and `quota`, `limit`, `used`,
  `resetsAt` problem extensions (`messageKey` `QuotaExceeded`).
- Units are refunded when the handler throws or answers 4xx/5xx; success carries `X-Quota-*` headers.
- `IQuotaService` counts from anywhere else too — jobs, message handlers, batch imports.
- In-memory counters serve one host; the Redis counters check and increment in one Lua script.
