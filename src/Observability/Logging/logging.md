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

Override defaults via `appsettings.json` (`Serilog:` section is read automatically).

## See also

- [Serilog docs](https://serilog.net/)
- [Destructurama.Attributed](https://github.com/destructurama/attributed) for `[NotLogged]` / `[LogMasked]` PII protection
