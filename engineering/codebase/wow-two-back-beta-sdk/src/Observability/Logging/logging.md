# WoW.Two.Sdk.Backend.Beta.Observability.Logging

> Serilog wired into `ILogger<T>` with sane defaults (Console + rolling File). Public seam is `ILogger<T>` — never depend on Serilog types in your app code.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Observability.Logging
```

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilogConventional();
// rest as normal — inject ILogger<T>
```

Both default sinks render the ambient trace id ahead of the message, so a log line joins its trace:

```
2026-08-24 16:26:53.693 +05:00 [INF] [e9421149ccb8147aec30ca2876e6e37a] Code deleted
```

The brackets are empty when no activity is in scope, such as during startup.

Override defaults via `appsettings.json` (`Serilog:` section is read automatically).

## See also

- [Serilog docs](https://serilog.net/)
- [Destructurama.Attributed](https://github.com/destructurama/attributed) for `[NotLogged]` / `[LogMasked]` PII protection
