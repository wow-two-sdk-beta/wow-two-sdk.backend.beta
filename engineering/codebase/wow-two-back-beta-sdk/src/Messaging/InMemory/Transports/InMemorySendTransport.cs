using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory.Transports;

/// <summary>Transports envelopes through the in-memory channel, deferring to the scheduler when an envelope carries a future delivery time.</summary>
/// <remarks>
///   - the receive side reads the same <see cref="EventEnvelopeModel"/> instance
///   - nothing is serialized
///   - every field survives by reference, <see cref="EventEnvelopeModel.ReplyTo"/> and <see cref="EventEnvelopeModel.ConversationId"/> included
/// </remarks>
internal sealed class InMemorySendTransport(InMemoryEventChannel channel, IDelayedDeliveryService scheduler, TimeProvider timeProvider) : ISendTransport
{
    public async ValueTask SendAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.NotBeforeUtc is { } notBefore && notBefore > timeProvider.GetUtcNow())
            await scheduler.ScheduleAsync(envelope, notBefore, cancellationToken);
        else
            await channel.Writer.WriteAsync(envelope, cancellationToken);
    }
}
