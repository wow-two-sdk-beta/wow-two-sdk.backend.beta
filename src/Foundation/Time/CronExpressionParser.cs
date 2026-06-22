using Cronos;

#pragma warning disable CA1200 // doc-id (T:) crefs are deliberate — Hangfire.Core bundles a second public Cronos namespace, so short crefs are ambiguous (CS0419)

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time;

/// <summary>Provides a thin wrapper around <see cref="T:Cronos.CronExpression"/> with conventional defaults.</summary>
public static class CronExpressionParser
{
    /// <summary>Parses a cron expression, accepting both 5-field (standard) and 6-field (with-seconds) forms via heuristic.</summary>
    /// <param name="expression">The cron expression to parse.</param>
    /// <exception cref="T:Cronos.CronFormatException">Invalid expression.</exception>
    public static CronExpression Parse(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var fieldCount = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
        return CronExpression.Parse(expression, format);
    }

    /// <summary>Computes the next occurrence after <paramref name="from"/> in <paramref name="zone"/>.</summary>
    /// <param name="expression">The cron expression to evaluate.</param>
    /// <param name="from">The instant to search forward from, exclusive.</param>
    /// <param name="zone">The time zone the schedule is interpreted in.</param>
    public static DateTimeOffset? NextOccurrence(string expression, DateTimeOffset from, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var expr = Parse(expression);
        return expr.GetNextOccurrence(from, zone);
    }
}
