using System.Globalization;
using Humanizer;

namespace WoW.Two.Sdk.Backend.Beta.Localization.Humanizing;

/// <summary>
/// Default <see cref="IRelativeTimeFormatter"/> — phrases an instant relative to "now" via Humanizer,
/// with "now" sourced from the injected <see cref="TimeProvider"/> so it is deterministic under test.
/// Stateless and thread-safe.
/// </summary>
public sealed class RelativeTimeFormatter : IRelativeTimeFormatter
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the formatter over the given time source.</summary>
    /// <param name="timeProvider">The clock used as the "now" reference point.</param>
    public RelativeTimeFormatter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public string Format(DateTimeOffset instant, CultureInfo? culture = null)
        => instant.Humanize(_timeProvider.GetUtcNow(), culture);
}
