using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Filter over a dead-letter store — the criteria an operator triages by. Every field is optional and they combine with
/// AND; a default instance matches everything the store holds, capped by <see cref="Limit"/>.
/// </summary>
public sealed record DeadLetterQuery
{
    /// <summary>
    /// Source destinations to search. Empty means every source — which needs an <see cref="IDeadLetterQueryRepository"/>,
    /// since the floor <see cref="IDeadLetterRepository.ReadAsync"/> enumerates one named source at a time.
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Only records dead-lettered at or after this instant.</summary>
    public DateTimeOffset? DeadLetteredAfterUtc { get; init; }

    /// <summary>Only records dead-lettered strictly before this instant.</summary>
    public DateTimeOffset? DeadLetteredBeforeUtc { get; init; }

    /// <summary>
    /// Terminal exception type. Matches either the full name (<c>System.TimeoutException</c>) or the simple name
    /// (<c>TimeoutException</c>), so an operator does not have to know the namespace to triage by failure kind.
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>Case-insensitive substring the failure reason must contain.</summary>
    public string? ReasonContains { get; init; }

    /// <summary>Only records redriven at least this many times — for finding messages that keep coming back.</summary>
    public int? MinRedriveCount { get; init; }

    /// <summary>Only records redriven at most this many times.</summary>
    public int? MaxRedriveCount { get; init; }

    /// <summary>Include quarantined records alongside the rest. Ignored when <see cref="QuarantinedOnly"/> is set.</summary>
    public bool IncludeQuarantined { get; init; }

    /// <summary>Match <b>only</b> quarantined records — the review queue.</summary>
    public bool QuarantinedOnly { get; init; }

    /// <summary>Maximum records to return. Defaults to 100; zero or negative means unbounded.</summary>
    public int Limit { get; init; } = 100;

    /// <summary>A query over one source with no other criteria.</summary>
    /// <param name="source">The source destination/queue name.</param>
    public static DeadLetterQuery ForSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return new DeadLetterQuery { Sources = [source] };
    }

    /// <summary>True when <paramref name="record"/> satisfies every criterion set on this query. <see cref="Limit"/> is not applied here — the caller counts.</summary>
    /// <param name="record">The record to test.</param>
    public bool Matches(DeadLetterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (QuarantinedOnly)
        {
            if (record.State != DeadLetterState.Quarantined)
                return false;
        }
        else if (record.State == DeadLetterState.Quarantined && !IncludeQuarantined)
        {
            return false;
        }

        if (Sources.Count > 0 && !Sources.Contains(record.Destination, StringComparer.Ordinal))
            return false;

        if (DeadLetteredAfterUtc is { } after && record.DeadLetteredAtUtc < after)
            return false;

        if (DeadLetteredBeforeUtc is { } before && record.DeadLetteredAtUtc >= before)
            return false;

        if (ExceptionType is { Length: > 0 } exceptionType && !MatchesExceptionType(record.ExceptionType, exceptionType))
            return false;

        if (ReasonContains is { Length: > 0 } reason && !record.Reason.Contains(reason, StringComparison.OrdinalIgnoreCase))
            return false;

        var redrives = record.EffectiveRedriveCount;
        if (MinRedriveCount is { } min && redrives < min)
            return false;

        return MaxRedriveCount is not { } max || redrives <= max;
    }

    private static bool MatchesExceptionType(string? recorded, string wanted)
    {
        if (recorded is null)
            return false;

        if (string.Equals(recorded, wanted, StringComparison.Ordinal))
            return true;

        // Simple-name match on a '.' boundary — "Exception" never matches "MyTimeoutException".
        return recorded.Length > wanted.Length
            && recorded.EndsWith(wanted, StringComparison.Ordinal)
            && recorded[recorded.Length - wanted.Length - 1] == '.';
    }
}
