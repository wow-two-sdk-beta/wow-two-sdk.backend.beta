using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>
/// NATS JetStream capability flags — no native DLQ (SDK emulates via a dead-letter subject); per-subject ordering;
/// PubAck on publish and core request-reply; no per-message priority, sessions or producer transactions, and the newer
/// per-message TTL / scheduling features are stream opt-ins this adapter does not enable. Acks are per-message and
/// thread-safe, so settlement runs from a pump worker and the consume loop has no affinity.
/// </summary>
internal sealed class NatsCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => false;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => true;

    /// <summary>
    /// No per-message priority. JetStream 2.11 priority groups rank pull-consumers competing for a stream (overflow /
    /// pinned-client policies), not messages within it — nothing lets one message overtake another.
    /// </summary>
    public bool NativePriority => false;

    /// <summary>
    /// Conditional, so reported false. A stream expires by its shared <c>MaxAge</c>; per-message TTL exists only as the
    /// <c>Nats-TTL</c> header on NATS Server 2.11+ AND only when the stream sets <c>AllowMsgTTL</c>. NatsTopologyBroker
    /// creates the stream without that flag, so a per-message TTL header would be ignored by the server.
    /// </summary>
    public bool NativeTimeToLive => false;

    /// <summary>
    /// Conditional, so reported false. Absolute-time delivery exists as the <c>Nats-Schedule</c> header on NATS Server
    /// 2.12+ AND only when the stream sets <c>AllowMsgSchedules</c> — an opt-in that cannot be reverted once enabled.
    /// NatsTopologyBroker does not set it, so a scheduled-delivery header would not be honoured.
    /// </summary>
    public bool NativeScheduling => false;

    /// <summary>No session or message-group affinity: a durable consumer is not pinned to a key, and JetStream has no group-scoped exclusive lock comparable to ASB sessions.</summary>
    public bool NativeSessions => false;

    /// <summary>JetStream publish returns a <c>PubAck</c> carrying the stream sequence — a real broker acknowledgement of persistence.</summary>
    public bool NativePublisherConfirms => true;

    /// <summary>No producer transactions or exactly-once semantics; publishes are acknowledged individually, with no atomic multi-subject commit.</summary>
    public bool NativeTransactions => false;

    /// <summary>Core NATS request-reply: the request carries an <c>_INBOX</c> reply subject the server routes back, with no reply queue to provision.</summary>
    /// <remarks>
    ///   - <c>IRequestClient</c> carries the reply address in <see cref="MessageHeaderConstants.ReplyTo"/>, never over an <c>_INBOX</c>
    ///   - an <c>_INBOX</c> subject sits outside the stream's subject list, so a reply there is rejected
    /// </remarks>
    public bool NativeRequestReply => true;

    /// <summary>JetStream acks are per-message and safe to send from a pump worker thread.</summary>
    public bool SettlesInContext => true;

    /// <summary>The consume loop is an ordinary async enumeration — no thread affinity to preserve.</summary>
    public bool ThreadAffineConsume => false;
}
