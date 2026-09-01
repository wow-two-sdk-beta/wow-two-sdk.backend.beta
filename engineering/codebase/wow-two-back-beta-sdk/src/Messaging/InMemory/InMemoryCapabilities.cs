using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>
/// In-memory capability flags — the scheduler gives real delayed and absolute-time delivery; dead-lettering and dedupe
/// are SDK-emulated, and the channel offers no per-key ordering guarantee once the pump runs workers. Everything a
/// broker would provide out-of-process (confirms, transactions, sessions, reply addresses) is absent by construction:
/// there is no broker, only a channel.
/// </summary>
internal sealed class InMemoryCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => false;

    public bool NativeDelay => true;

    public bool NativeDedupe => false;

    public bool NativeOrdering => false;

    /// <summary>The backing channel is plain FIFO — nothing reorders a queued envelope ahead of another.</summary>
    public bool NativePriority => false;

    /// <summary>The channel never expires an entry; a queued envelope is delivered however stale it has become.</summary>
    public bool NativeTimeToLive => false;

    /// <summary>
    /// The send path defers to IDelayedDeliveryService, which takes an absolute UTC instant (<c>NotBeforeUtc</c>) rather than a
    /// relative offset. In-process only: the schedule lives as long as the host does unless the scheduler is backed by a
    /// durable store.
    /// </summary>
    public bool NativeScheduling => true;

    /// <summary>No session or group affinity — the pump's workers pull from one shared channel with no key-to-worker binding.</summary>
    public bool NativeSessions => false;

    /// <summary>A channel write is an in-process enqueue, not an out-of-process durable acknowledgement — a successful send proves only that the writer accepted it.</summary>
    public bool NativePublisherConfirms => false;

    /// <summary>No producer transactions — a batch of writes has no atomic commit or rollback.</summary>
    public bool NativeTransactions => false;

    /// <summary>
    /// No reply-address mechanism of its own: the channel is fire-and-forget and has no inbox or pseudo-queue to route
    /// a response back through. <c>IRequestClient</c> still works here — the envelope carries its reply address and
    /// conversation id in-process, and the response is an ordinary write to the same channel — but that is the SDK
    /// correlating, which is exactly what this flag says the transport does not do.
    /// </summary>
    public bool NativeRequestReply => false;

    public bool SettlesInContext => true;

    public bool ThreadAffineConsume => false;
}
