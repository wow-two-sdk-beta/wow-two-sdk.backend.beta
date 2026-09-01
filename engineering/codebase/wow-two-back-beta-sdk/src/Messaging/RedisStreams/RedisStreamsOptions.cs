using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>Options for the Redis Streams event-bus adapter.</summary>
public sealed record RedisStreamsOptions
{
    /// <summary>StackExchange.Redis configuration string. Default <c>localhost:6379</c>.</summary>
    public string Configuration { get; set; } = "localhost:6379";

    /// <summary>Redis database index. Default <c>-1</c> (the connection's configured default). Redis Cluster only serves database 0.</summary>
    public int Database { get; set; } = -1;

    /// <summary>Stream key events are added to / read from. Default <c>wt.events</c>.</summary>
    public string Stream { get; set; } = "wt.events";

    /// <summary>
    /// Consumer group. Every instance of one service joins the same group so they compete for entries
    /// (<c>XREADGROUP</c> hands each entry to exactly one member) rather than each receiving everything. Default
    /// <c>wt-consumers</c>.
    /// </summary>
    public string ConsumerGroup { get; set; } = "wt-consumers";

    /// <summary>
    /// This instance's consumer name within <see cref="ConsumerGroup"/> — the owner of its pending-entries list (PEL).
    /// Null derives <c>{machine}-{pid}</c>, which is unique per process and stable for its lifetime.
    /// </summary>
    /// <remarks>
    ///   - Never share a name across instances — a shared PEL lets one claim an entry the other is still processing
    ///   - a dead process leaves its PEL under its old name, recovered by the claim path
    ///   - Run <c>XGROUP DELCONSUMER</c> only against an empty PEL — it discards pending entries rather than returning them
    /// </remarks>
    public string? ConsumerName { get; set; }

    /// <summary>Dead-letter stream — Redis has no native dead-letter facility, so exhausted/poison entries are re-added here (emulated DLQ). Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterStream { get; set; } = "wt.events.dlq";

    /// <summary>
    /// Cap on entries kept in a stream, applied on every <c>XADD</c> (<c>MAXLEN</c>). Default 100 000; null keeps the
    /// stream untrimmed and therefore unbounded — a stream is an append-only log and acknowledging an entry does not
    /// remove it, so without a cap the key grows until Redis evicts or OOMs.
    /// </summary>
    /// <remarks>
    ///   - trimming can evict an entry still pending in some consumer's PEL
    ///   - the claim path acknowledges the phantom row, so the cost is a lost message rather than a stuck one
    ///   - size the cap above the worst-case backlog — the message really is gone
    /// </remarks>
    public int? MaxLength { get; set; } = 100_000;

    /// <summary>
    /// Trim approximately (<c>MAXLEN ~ N</c>) rather than exactly. Default true — approximate trimming stops at a macro
    /// node boundary and is amortized O(1), where exact trimming walks the entries it removes. The stream then sits at
    /// or slightly above <see cref="MaxLength"/>, never below.
    /// </summary>
    public bool UseApproximateMaxLength { get; set; } = true;

    /// <summary>
    /// Cap on the dead-letter stream. Default null (never trimmed): a dead letter is the only remaining copy of a
    /// message the system failed to process, and dropping it to bound a key trades a visible backlog for silent data
    /// loss. Draining the DLQ is an operator action; set this only where that trade is understood.
    /// </summary>
    public int? DeadLetterMaxLength { get; set; }

    /// <summary>Maximum entries fetched per stream per read, and per claim sweep (<c>COUNT</c>). Default 32.</summary>
    public int BatchSize { get; set; } = 32;

    /// <summary>
    /// How long the consume loop waits after an empty read before polling again. Default 250 ms.
    /// </summary>
    /// <remarks>
    ///   - the loop polls — StackExchange.Redis multiplexes every command, so <c>XREADGROUP</c> takes no <c>BLOCK</c>
    ///   - this interval is therefore the adapter's idle latency floor
    ///   - applies only when a read came back empty, so a busy stream drains back-to-back with no delay
    /// </remarks>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>How often the consume loop sweeps the pending-entries list for stale entries to claim. Default 30 s. The first sweep runs at startup, which is when orphaned entries are most likely.</summary>
    public TimeSpan ClaimInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long an entry must sit unacknowledged in a consumer's PEL before another consumer may claim it. Default
    /// 5 minutes.
    /// </summary>
    /// <remarks>
    ///   - idle time is measured from the last delivery, not from the last sign of life
    ///   - a handler still running after this long is claimed underneath and runs twice concurrently
    ///   - keep it above the slowest handler's worst case — the inbox dedupes the replay, not the double run
    /// </remarks>
    public TimeSpan MinIdleTimeBeforeClaim { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Deliveries an entry may accumulate in the PEL before the claim path dead-letters it instead of redelivering.
    /// Default 5. Prevents a message that kills its consumer from being claimed forever.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Route each message to the stream <see cref="ITopologyService"/> resolves from it — the message type's stable
    /// token for a publish, <see cref="EventEnvelope.Destination"/> for an explicit
    /// <see cref="IEventBus.SendAsync{TEvent}"/> — instead of adding everything to <see cref="Stream"/>. Routed streams
    /// are nested under <see cref="Stream"/> (<c>wt.events.order-placed</c>). Default false, which keeps an existing
    /// deployment on its single stream.
    /// </summary>
    /// <remarks>
    ///   - Migrate consumers first — a consumer with this on reads <see cref="Stream"/> as well as every routed stream
    ///   - on Redis Cluster one <c>XREADGROUP</c> over several streams needs every key in one slot, else CROSSSLOT
    ///   - give <see cref="Stream"/> a hash tag (<c>{wt.events}</c>) — the adapter neither adds nor assumes one
    /// </remarks>
    public bool RouteByDestination { get; set; }
}
