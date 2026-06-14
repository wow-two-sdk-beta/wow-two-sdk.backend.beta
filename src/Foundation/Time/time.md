# WoW.Two.Sdk.Backend.Beta.Time

> Time abstractions — `TimeProvider` defaults, NodaTime adapter, time-zone resolution (Windows↔IANA), cron parsing.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Time
```

## Usage

### Registration

```csharp
builder.Services.AddTimeProviders();
```

### Resolve a time zone (any id format)

```csharp
var tz = TimeZoneHelpers.ResolveTimeZone("America/New_York"); // works on Windows
var tz2 = TimeZoneHelpers.ResolveTimeZone("Eastern Standard Time"); // works on Linux
```

### Cron expressions

```csharp
var expr = CronExpressionParser.Parse("*/15 * * * *");
var next = CronExpressionParser.NextOccurrence(
    "0 0 8 * * *",
    DateTimeOffset.UtcNow,
    TimeZoneInfo.FindSystemTimeZoneById("UTC"));
```

## See also

- [NodaTime](https://nodatime.org/)
- [TimeZoneConverter](https://github.com/mattjohnsonpint/TimeZoneConverter)
- [Cronos](https://github.com/HangfireIO/Cronos)
