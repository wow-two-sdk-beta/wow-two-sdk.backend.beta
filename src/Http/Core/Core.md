# WoW.Two.Sdk.Backend.Beta.Http

> Outbound HTTP — declarative Refit clients and plain typed clients, both wrapped in the standard Polly v8 resilience pipeline. The SDK's answer to "calling other services."

Namespaces:
- `WoW.Two.Sdk.Backend.Beta.Http.Refit` — declarative API clients
- `WoW.Two.Sdk.Backend.Beta.Http.Resilience` — the resilience handler + options
- `WoW.Two.Sdk.Backend.Beta.Http.Core` — plain typed / named clients

## Refit client (default)

```csharp
public interface IBillingApi
{
    [Get("/invoices/{id}")] Task<Invoice> GetInvoice(string id);
    [Post("/invoices")]     Task<Invoice> CreateInvoice([Body] NewInvoice body);
}

builder.Services.AddRefitApiClient<IBillingApi>("https://billing.internal");
```

That single call wires:
- **SDK JSON** (`JsonOptionsPresets.Default` — camelCase, NodaTime, lenient input) via `SystemTextJsonContentSerializer`
- **Base address**
- **Standard resilience** — retry → circuit breaker → per-attempt timeout, inside a total-request timeout

Tune resilience or the client inline:

```csharp
builder.Services.AddRefitApiClient<IBillingApi>(
    "https://billing.internal",
    configureResilience: r => { r.MaxRetryAttempts = 5; r.TotalRequestTimeout = TimeSpan.FromSeconds(60); },
    configureClient: c => c.DefaultRequestHeaders.Add("X-Tenant", "acme"));
```

## Plain typed / named client (no Refit)

```csharp
// typed client class
builder.Services.AddResilientClient<WeatherClient>(new Uri("https://weather.example.com"));

// named client (resolve via IHttpClientFactory)
builder.Services.AddResilientClient("github", new Uri("https://api.github.com"));
```

## Resilience defaults

`HttpResilienceOptions` — overridable per client:

| Option | Default | Meaning |
|---|---|---|
| `MaxRetryAttempts` | 3 | retries after the first try |
| `AttemptTimeout` | 10s | per-attempt timeout |
| `TotalRequestTimeout` | 30s | budget for the whole logical request incl. retries |
| `CircuitBreakerSamplingDuration` | 30s | failure-rate window (≥ 2× attempt timeout) |
| `CircuitBreakerFailureRatio` | 0.1 | trip threshold (10%) |

Backed by `Microsoft.Extensions.Http.Resilience` (Polly v8). OTel HttpClient instrumentation is wired by the observability package, so these calls are traced automatically.

## See also

- `…Observability.Tracing` — outbound spans for every call
- `…Serialization` — the `JsonOptionsPresets` used by Refit clients
- [Refit](https://github.com/reactiveui/refit) · [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
