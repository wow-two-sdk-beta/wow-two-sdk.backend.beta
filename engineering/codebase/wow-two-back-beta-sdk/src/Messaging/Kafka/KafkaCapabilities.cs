using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Kafka;

/// <summary>
/// Kafka capability flags — no native DLQ (SDK emulates via a dead-letter topic); per-partition ordering; broker acks
/// and producer transactions (EOS); no per-message priority, TTL, scheduling, sessions or reply-address mechanism;
/// consume/settle is thread-affine to the consume loop, so dispatch stays sequential.
/// </summary>
internal sealed class KafkaCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => false;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => true;

    /// <summary>Kafka has no per-message priority on the wire at all — a partition is a strict append-only log, so a later message can never overtake an earlier one.</summary>
    public bool NativePriority => false;

    /// <summary>
    /// Kafka expires by topic-wide retention (<c>retention.ms</c> / <c>retention.bytes</c>), which ages every record in
    /// the topic on one shared clock. There is no per-record expiry, so an envelope's TimeToLive cannot be honoured.
    /// </summary>
    public bool NativeTimeToLive => false;

    /// <summary>No broker-side scheduled delivery: a produced record is immediately fetchable, and holding it back would have to be emulated consumer-side.</summary>
    public bool NativeScheduling => false;

    /// <summary>
    /// Partition keys give ordering (already reported by NativeOrdering) but not sessions — there is no exclusive
    /// per-group consumer lock or group-scoped state; a rebalance can hand a key's partition to another consumer.
    /// </summary>
    public bool NativeSessions => false;

    /// <summary>The <c>acks</c> setting plus the offset returned from produce is a real broker acknowledgement of durable acceptance.</summary>
    public bool NativePublisherConfirms => true;

    /// <summary>
    /// Kafka EOS — <c>transactional.id</c> with begin/commit and offsets committed inside the transaction. The
    /// capability is native; the adapter's producer is not configured with a <c>transactional.id</c> today, so nothing
    /// currently opens a transaction.
    /// </summary>
    public bool NativeTransactions => true;

    /// <summary>No reply-address mechanism: request-reply over Kafka means provisioning a reply topic and correlating by header, which is the SDK's work, not the broker's.</summary>
    public bool NativeRequestReply => false;

    /// <summary>Offsets advance on the consume loop, not through the receive context — a worker thread must not settle out of band.</summary>
    public bool SettlesInContext => false;

    /// <summary>librdkafka's consumer is not thread-safe: <c>Consume</c> and <c>StoreOffset</c> must stay on the loop thread, so the pump keeps dispatch sequential.</summary>
    public bool ThreadAffineConsume => true;
}
