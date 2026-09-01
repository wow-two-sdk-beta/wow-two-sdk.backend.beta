using Cronos;

#pragma warning disable CA1200 // doc-id (T:) crefs are deliberate — Hangfire.Core bundles a second public Cronos namespace, so short crefs are ambiguous (CS0419)

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time;

/// <summary>Provides a thin wrapper around <see cref="T:Cronos.CronExpression"/> with conventional defaults.</summary>
public interface ICronExpressionParser
{
    /// <summary>Parses a cron expression, accepting both 5-field (standard) and 6-field (with-seconds) forms.</summary>
    /// <param name="expression">The cron expression to parse.</param>
    CronExpression Parse(string expression);

    /// <summary>Gets the next occurrence of <paramref name="expression"/> after <paramref name="from"/> in <paramref name="zone"/>.</summary>
    /// <param name="expression">The cron expression to evaluate.</param>
    /// <param name="from">The instant to search forward from.</param>
    /// <param name="zone">The time zone the schedule is written in.</param>
    DateTimeOffset? NextOccurrence(string expression, DateTimeOffset from, TimeZoneInfo zone);
}
