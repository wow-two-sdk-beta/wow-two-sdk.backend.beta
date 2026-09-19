using Cronos;

#pragma warning disable CA1200 // doc-id (T:) crefs are deliberate — Hangfire.Core bundles a second public Cronos namespace, so short crefs are ambiguous (CS0419)

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time.Parsers;

/// <summary>Parses cron expressions through Cronos, accepting the 5-field and 6-field forms.</summary>
public sealed class CronExpressionParser : ICronExpressionParser
{
    /// <inheritdoc />
    public CronExpression Parse(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var fieldCount = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
        return CronExpression.Parse(expression, format);
    }
}
