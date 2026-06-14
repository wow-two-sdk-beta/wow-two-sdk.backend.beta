# WoW.Two.Sdk.Backend.Beta.Observability.Tracing

> OpenTelemetry tracer wiring with conventional auto-instrumentation. Exporters live in sibling packages.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Tracing
# Pair with one or more exporters:
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Otlp
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.AzureMonitor
```

## Usage

```csharp
builder.Services.AddOpenTelemetryTracing("my-service");
builder.Services.AddOtlpExporters("https://collector:4317");
```

Auto-instrumented sources: ASP.NET Core · HttpClient · gRPC client · SqlClient · EF Core · StackExchange.Redis.

## See also

- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
