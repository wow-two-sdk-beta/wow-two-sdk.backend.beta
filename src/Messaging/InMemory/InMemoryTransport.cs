using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>In-memory <see cref="ISendTransport"/> — writes the envelope to the channel, deferring to the scheduler when it carries a future delivery time.</summary>
/// <remarks>
/// There is no wire format to map onto: the same <see cref="EventEnvelope"/> instance the bus built is the one the
/// receive side reads, so every field — including <see cref="EventEnvelope.ReplyTo"/> and
/// <see cref="EventEnvelope.ConversationId"/> — survives by reference. This is why <c>IRequestClient</c> is exercised
/// here first: the correlation logic is isolated from any adapter's header mapping.
/// </remarks>
internal sealed class InMemorySendTransport(InMemoryEventChannel channel, IEventScheduler scheduler, TimeProvider timeProvider) : ISendTransport
{
    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.NotBeforeUtc is { } notBefore && notBefore > timeProvider.GetUtcNow())
            await scheduler.ScheduleAsync(envelope, notBefore, cancellationToken);
        else
            await channel.Writer.WriteAsync(envelope, cancellationToken);
    }
}

/// <summary>In-memory <see cref="ReceiveContext"/> — acknowledge is a no-op (the channel read already removed it); dead-letter writes to the <see cref="IDeadLetterStore"/>.</summary>
internal sealed class InMemoryReceiveContext(EventEnvelope envelope, IDeadLetterStore deadLetters, TimeProvider timeProvider) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        var record = new DeadLetterRecord(envelope.MessageId, envelope.Destination, reason, exception?.GetType().FullName, envelope, timeProvider.GetUtcNow());
        return deadLetters.DeadLetterAsync(record, cancellationToken);
    }
}

/// <summary>In-memory <see cref="IReceiveTransport"/> — reads the channel and hands each envelope to the pipeline until the host stops.</summary>
internal sealed class InMemoryReceiveTransport(InMemoryEventChannel channel, IDeadLetterStore deadLetters, TimeProvider timeProvider) : IReceiveTransport
{
    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);
        try
        {
            await foreach (var envelope in channel.Reader.ReadAllAsync(cancellationToken))
                await onMessage(new InMemoryReceiveContext(envelope, deadLetters, timeProvider), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

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
    /// The send path defers to IEventScheduler, which takes an absolute UTC instant (<c>NotBeforeUtc</c>) rather than a
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
