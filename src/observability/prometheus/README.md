# WoW.Two.Sdk.Backend.Beta.Observability.Prometheus

> Prometheus scrape exporter for OTel metrics.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Prometheus
```

## Usage

```csharp
builder.Services.AddWowTwoMetrics("my-service");
builder.Services.AddWowTwoPrometheusExporter();

var app = builder.Build();
app.MapPrometheusScrapingEndpoint();   // exposes /metrics by default
```
