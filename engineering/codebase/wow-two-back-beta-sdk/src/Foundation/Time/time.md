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

The registered NodaTime `IClock` reads from the same `TimeProvider`. Supplying a fake
`TimeProvider` therefore controls both clock surfaces.

### Resolve a time zone (any id format)

```csharp
var tz = TimeZoneMapper.ResolveTimeZone("America/New_York"); // works on Windows
var tz2 = TimeZoneMapper.ResolveTimeZone("Eastern Standard Time"); // works on Linux
```

### Cron expressions

```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Time.Parsers;

ICronExpressionParser parser = new CronExpressionParser();
var expression = parser.Parse("0 0 8 * * *");
var next = expression.GetNextOccurrence(
    DateTimeOffset.UtcNow,
    TimeZoneInfo.FindSystemTimeZoneById("UTC"));
```

`ICronExpressionParser` and `CronExpressionParser` live under `Time/Parsers/`.
Six space-separated fields select the seconds form; other field counts use the standard form.
Null or blank input is rejected by argument checks; invalid or incomplete cron syntax throws `CronFormatException`.
The parser returns a complete expression or throws. Occurrence calculation belongs to the returned expression.

## See also

- [NodaTime](https://nodatime.org/)
- [TimeZoneConverter](https://github.com/mattjohnsonpint/TimeZoneConverter)
- [Cronos](https://github.com/HangfireIO/Cronos)
