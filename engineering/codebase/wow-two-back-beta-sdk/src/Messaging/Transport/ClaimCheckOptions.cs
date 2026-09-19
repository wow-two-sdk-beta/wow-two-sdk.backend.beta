using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Holds settings that move a body too large for the broker into blob storage and send a pointer instead, rehydrating it before the handler
/// sees it. Every broker caps message size (Azure Service Bus Standard and SQS at 256 KB, Kafka at 1 MB by default), and
/// a body over the cap is rejected at publish with nothing the SDK can do about it.
/// </summary>
/// <remarks>
///   - register an <see cref="IBlobRepository"/> alongside this (<c>AddLocalBlobStorage</c> or a cloud adapter)
///   - blobs expire under <see cref="Retention"/> only, never on consume
///   - set <see cref="Retention"/> past the retry ladder plus dead-letter triage, or a redrive rehydrates nothing
/// </remarks>
public sealed record ClaimCheckOptions
{
    /// <summary>
    /// Offload bodies over <see cref="ThresholdBytes"/> instead of sending them inline. Defaults to <c>false</c>;
    /// <c>AddEventClaimCheck(...)</c> turns it on before applying the caller's configuration, so a bound configuration
    /// section that omits the key cannot switch the wire format over by accident — but can still switch that call back off.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Serialized-body size above which the body is offloaded. Default 128 KiB — under the tightest common broker cap
    /// (256 KB) with room left for headers, so a message that passes the check also passes the broker.
    /// </summary>
    public int ThresholdBytes { get; set; } = 128 * 1024;

    /// <summary>Logical blob path prefix every claim-checked body is written under. A reference pointing outside it is refused on rehydrate.</summary>
    public string PathPrefix { get; set; } = "messaging/claim-check";

    /// <summary>
    /// How long an offloaded body is kept before the sweep deletes it. Default 7 days. This is the redrive window — a
    /// dead-lettered message replayed after it expires cannot be rehydrated and dead-letters again.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Run the retention sweep in-process. Default <c>true</c>; turn it off when the blob store expires objects itself.</summary>
    public bool SweepEnabled { get; set; } = true;

    /// <summary>How often the retention sweep runs. Default 1 hour.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Largest blob the rehydrator will read back. Default 64 MiB. A reference is wire data and may be corrupt or
    /// hostile; without a ceiling one bad pointer at a huge object takes the consumer down with an out-of-memory.
    /// </summary>
    public long MaxPayloadBytes { get; set; } = 64L * 1024 * 1024;
}
