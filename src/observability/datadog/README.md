# WoW.Two.Sdk.Backend.Beta.Observability.Datadog

> Datadog APM tracer (alternative to the OTel exporter route).

> **Note**: Datadog supports both native tracer (`Datadog.Trace`) and OpenTelemetry-OTLP intake. For OTel-native, use `Observability.Otlp` and point at the Datadog Agent's OTLP receiver. Use this package only when the native tracer features are required.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Datadog
```

## Usage

The native tracer auto-instruments via the `dd-trace-dotnet` profiler when run inside a `dd-trace`-instrumented host (Kubernetes operator, IIS module, or `dd-trace run -- dotnet ...`). This package vendors `Datadog.Trace` so manual `Tracer.Instance` calls compile.

For most teams, prefer the OTLP route:

```csharp
builder.Services.AddWowTwoTracing("my-service");
builder.Services.AddWowTwoOtlpExporter(new Uri("http://localhost:4317"));   // dd-agent OTLP
```

## See also

- [Datadog .NET tracer](https://docs.datadoghq.com/tracing/trace_collection/library_config/dotnet-core/)
