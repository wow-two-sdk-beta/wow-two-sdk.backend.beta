using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>In-memory <see cref="ISendTransport"/> — writes the envelope to the channel, deferring to the scheduler when it carries a future delivery time.</summary>
/// <remarks>
///   - the receive side reads the same <see cref="EventEnvelope"/> instance
///   - nothing is serialized
///   - every field survives by reference, <see cref="EventEnvelope.ReplyTo"/> and <see cref="EventEnvelope.ConversationId"/> included
/// </remarks>
internal sealed class InMemorySendTransport(InMemoryEventChannel channel, IDelayedDeliveryService scheduler, TimeProvider timeProvider) : ISendTransport
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
