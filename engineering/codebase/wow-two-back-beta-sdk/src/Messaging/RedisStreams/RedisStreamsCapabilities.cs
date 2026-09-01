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

/// <summary>
/// Redis Streams capability flags. A stream is an ordered log with consumer groups over it, so the honest answers are
/// clustered at "no broker feature, the SDK emulates it": no dead-letter facility, no delay or scheduling, no priority,
/// no per-message TTL, no sessions, no producer transactions, no reply address. What it does have is a real
/// pending-entries list, which makes redelivery counting and stale-message recovery first-class rather than emulated,
/// and settlement that is safe from any thread.
/// </summary>
internal sealed class RedisStreamsCapabilities : ITransportCapabilities
{
    /// <summary>No dead-letter facility of any kind — the SDK emulates one by adding the entry to a separate stream and acking the original.</summary>
    public bool NativeDeadLetter => false;

    /// <summary>No delayed delivery: an <c>XADD</c>ed entry is immediately readable. Holding it back means a sorted set keyed by due time and a mover — an emulation, not a broker feature.</summary>
    public bool NativeDelay => false;

    /// <summary>
    /// No dedupe by message id. <c>XADD</c> with an explicit id rejects one at or below the stream's tail, but that
    /// enforces id monotonicity, not idempotency — it cannot recognize a re-send of an entry already deeper in the log.
    /// </summary>
    public bool NativeDedupe => false;

    /// <summary>A stream is a strictly ordered append-only log and entries are delivered to a group in id order. Ordering is per stream, and (as on every competing-consumer broker) it is the read that stays ordered, not the concurrent processing of what was read.</summary>
    public bool NativeOrdering => true;

    /// <summary>No per-message priority. Entries are ranked by id — that is, by insertion time — so nothing can overtake anything already in the stream.</summary>
    public bool NativePriority => false;

    /// <summary>
    /// Conditional, so reported false. <c>MAXLEN</c> and <c>MINID</c> bound a whole stream on one shared policy, which
    /// is the retention shape this flag explicitly excludes; there is no per-entry deadline, and a key-level
    /// <c>EXPIRE</c> would take the entire stream with it rather than one message.
    /// </summary>
    public bool NativeTimeToLive => false;

    /// <summary>No absolute-time enqueue. Entry ids are timestamps of when the entry was written, not of when it becomes readable.</summary>
    public bool NativeScheduling => false;

    /// <summary>
    /// No session or group affinity. A consumer owns individual entries through its pending-entries list, not a key
    /// range — two messages sharing a partition key land on whichever consumers <c>XREADGROUP</c> hands them to, and
    /// nothing locks a group of related messages to one consumer.
    /// </summary>
    public bool NativeSessions => false;

    /// <summary>
    /// <c>XADD</c> is a synchronous command that answers with the assigned entry id, so the send path learns the entry
    /// was accepted into the stream rather than merely written to a socket.
    /// </summary>
    /// <remarks>
    ///   - acceptance is not fsync — Redis persists (<c>appendfsync everysec</c>) and replicates asynchronously
    ///   - a master dying inside that window loses acknowledged entries
    ///   - closing the gap is <c>WAIT</c> / <c>appendfsync always</c>, which this adapter does not impose
    /// </remarks>
    public bool NativePublisherConfirms => true;

    /// <summary>
    /// <c>MULTI</c>/<c>EXEC</c> gives atomic batch execution only — no idempotent producer, no dedupe across a retry,
    /// and no consume-transform-produce atomicity — which is precisely the shape this flag excludes.
    /// </summary>
    public bool NativeTransactions => false;

    /// <summary>No reply-address mechanism. Request-reply over streams means provisioning a reply stream and correlating by field, which is the SDK's work, not Redis'.</summary>
    public bool NativeRequestReply => false;

    /// <summary><c>XACK</c> is an ordinary command on a thread-safe multiplexer, so settlement is safe from a pump worker thread.</summary>
    public bool SettlesInContext => true;

    /// <summary>The consume loop is a poll over an async API with no thread ownership — nothing has to stay on the loop thread.</summary>
    public bool ThreadAffineConsume => false;
}
