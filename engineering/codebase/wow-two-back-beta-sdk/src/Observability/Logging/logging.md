# WoW.Two.Sdk.Backend.Beta.Observability.Logging

> Serilog wired into `ILogger<T>` with sane defaults (Console + rolling File). Public seam is `ILogger<T>` — never depend on Serilog types in your app code.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Logging
```

## Usage

```csharp
using WoW.Two.Sdk.Backend.Beta.Observability.Logging.Services;

await new StartupFailureReportingService().RunAsync(async cancellationToken =>
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilogConventional();

    var app = builder.Build();
    await app.RunAsync(cancellationToken);
});
```

`StartupFailureReportingService` creates `logs/startup-failures.log` before the builder. An exception from builder creation, configuration binding, options validation, build or start is written there, logging is flushed and the original exception escapes so the process exits nonzero. `UseSerilogConventional` remains the separate final application logger.

Both default sinks render the ambient trace id ahead of the message, so a log line joins its trace:

```
2026-08-24 16:26:53.693 +05:00 [INF] [e9421149ccb8147aec30ca2876e6e37a] Code deleted
```

The brackets are empty when no activity is in scope, such as during startup.

Override defaults via `appsettings.json` (`Serilog:` section is read automatically).

## See also

- [Serilog docs](https://serilog.net/)
- [Destructurama.Attributed](https://github.com/destructurama/attributed) for `[NotLogged]` / `[LogMasked]` PII protection
