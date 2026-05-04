# WoW.Two.Sdk.Backend.Beta.Observability.Metrics

> OpenTelemetry meter wiring with conventional auto-instrumentation.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Metrics
```

## Usage

```csharp
builder.Services.AddWowTwoMetrics("my-service");
builder.Services.AddWowTwoOtlpExporter("https://collector:4317");
```

Auto-instrumented: ASP.NET Core · HttpClient · .NET runtime (GC, threadpool, JIT) · Process (CPU, memory).

## See also

- [OpenTelemetry .NET metrics](https://opentelemetry.io/docs/languages/net/instrumentation/#metrics)
