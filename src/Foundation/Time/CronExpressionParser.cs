using Cronos;

namespace WoW.Two.Sdk.Backend.Beta.Time;

/// <summary>
/// Thin wrapper around <see cref="CronExpression"/> with conventional defaults.
/// </summary>
public static class CronExpressionParser
{
    /// <summary>
    /// Parse a cron expression. Accepts both 5-field (standard) and 6-field (with-seconds) forms via heuristic.
    /// </summary>
    /// <exception cref="CronFormatException">Invalid expression.</exception>
    public static CronExpression Parse(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var fieldCount = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
        return CronExpression.Parse(expression, format);
    }

    /// <summary>
    /// Compute the next occurrence after <paramref name="from"/> in <paramref name="zone"/>.
    /// </summary>
    public static DateTimeOffset? NextOccurrence(string expression, DateTimeOffset from, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var expr = Parse(expression);
        return expr.GetNextOccurrence(from, zone);
    }
}
