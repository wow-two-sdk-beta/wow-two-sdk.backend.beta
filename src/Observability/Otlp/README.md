# WoW.Two.Sdk.Backend.Beta.Observability.Otlp

> OTLP exporter for OpenTelemetry traces + metrics. Pair with `Observability.Tracing` and `Observability.Metrics`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Otlp
```

## Usage

```csharp
builder.Services.AddOpenTelemetryTracing("my-service");
builder.Services.AddOpenTelemetryMetrics("my-service");
builder.Services.AddOtlpExporters(new Uri("http://collector:4317"));
```

Honors `OTEL_EXPORTER_OTLP_ENDPOINT` env var when no endpoint is passed.

## See also

- [OTLP spec](https://opentelemetry.io/docs/specs/otlp/)
