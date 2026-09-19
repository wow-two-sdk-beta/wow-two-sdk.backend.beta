using Cronos;

#pragma warning disable CA1200 // doc-id (T:) crefs are deliberate — Hangfire.Core bundles a second public Cronos namespace, so short crefs are ambiguous (CS0419)

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time.Parsers;

/// <summary>Defines cron syntax parsing into a <see cref="T:Cronos.CronExpression"/>.</summary>
public interface ICronExpressionParser
{
    /// <summary>Parses a cron expression, accepting both 5-field (standard) and 6-field (with-seconds) forms.</summary>
    /// <param name="expression">The cron expression to parse.</param>
    /// <returns>The parsed expression; occurrence calculation is performed on this value.</returns>
    /// <remarks>
    /// Six space-separated fields select the with-seconds format; all other field counts use the standard format.
    /// Invalid or incomplete syntax is rejected without returning a partial expression.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The expression is null.</exception>
    /// <exception cref="ArgumentException">The expression is empty or whitespace.</exception>
    /// <exception cref="T:Cronos.CronFormatException">The expression is invalid for the selected format.</exception>
    CronExpression Parse(string expression);
}
