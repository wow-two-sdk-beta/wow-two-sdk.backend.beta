# WoW.Two.Sdk.Backend.Beta.Observability.AzureMonitor

> Azure Monitor (Application Insights) distro for OpenTelemetry.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.AzureMonitor
```

## Usage

```csharp
builder.Services.AddAzureMonitorExporter();   // reads APPLICATIONINSIGHTS_CONNECTION_STRING
// or pass explicitly
builder.Services.AddAzureMonitorExporter("InstrumentationKey=...");
```
