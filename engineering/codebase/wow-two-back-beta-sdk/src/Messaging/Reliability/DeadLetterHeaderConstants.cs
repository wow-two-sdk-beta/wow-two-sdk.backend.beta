using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Reserved wire headers that carry dead-letter administration state on the envelope itself, so it survives the round
/// trip through the broker and back into the store.
/// </summary>
/// <remarks>
///   - the redrive cap holds across replays and restarts because the count rides on the envelope
///   - a message that dies again gets a fresh <see cref="DeadLetterRecord"/>, carrying nothing from the previous one
/// </remarks>
public static class DeadLetterHeaderConstants
{
    /// <summary>Reserved. How many times this message has been redriven out of a dead-letter store.</summary>
    public const string RedriveCount = MessageHeaderConstants.ReservedPrefix + "dl-redrive-count";

    /// <summary>Reserved. Round-trip (<c>o</c>) timestamp of the most recent redrive.</summary>
    public const string RedrivenAt = MessageHeaderConstants.ReservedPrefix + "dl-redriven-at";

    /// <summary>Read the redrive marker off an envelope; 0 when absent or unparseable.</summary>
    /// <param name="envelope">The envelope to inspect.</param>
    public static int ReadRedriveCount(EventEnvelope? envelope)
    {
        if (envelope?.Headers is not { Count: > 0 } headers || !headers.TryGetValue(RedriveCount, out var raw))
            return 0;

        // A malformed marker reads as "never redriven" — a bad header under-counts, it never fails the browse.
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : 0;
    }

    /// <summary>Return a copy of <paramref name="envelope"/> stamped as redriven — marker bumped, delivery count and delay reset.</summary>
    /// <param name="envelope">The envelope being redriven.</param>
    /// <param name="redriveCount">The new redrive count.</param>
    /// <param name="redrivenAtUtc">When the redrive happened.</param>
    public static EventEnvelope StampRedrive(EventEnvelope envelope, int redriveCount, DateTimeOffset redrivenAtUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var headers = new Dictionary<string, string>(envelope.Headers, StringComparer.Ordinal)
        {
            [RedriveCount] = redriveCount.ToString(CultureInfo.InvariantCulture),
            [RedrivenAt] = redrivenAtUtc.ToString("o", CultureInfo.InvariantCulture),
        };

        return envelope with
        {
            // A redrive is a fresh start for the retry budget — the whole point of putting the message back.
            DeliveryCount = 0,

            // Clear the delivery time so a stale future instant cannot re-park the message.
            NotBeforeUtc = null,
            Headers = headers,
        };
    }
}
