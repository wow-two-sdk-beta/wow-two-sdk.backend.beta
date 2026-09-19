using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Defines the send half of a transport — publishes an <see cref="EventEnvelopeModel"/> to the underlying medium (in-memory channel,
/// RabbitMQ exchange, Kafka topic, …). <see cref="IEventBus"/> sits over this: it builds the envelope, the transport
/// owns the wire format. A CAP adapter implements this cleanly; the in-memory transport just writes its channel.
/// </summary>
public interface ISendTransport
{
    /// <summary>Publish an envelope. The transport serializes <see cref="EventEnvelopeModel.Body"/> and maps headers/routing as it sees fit.</summary>
    /// <param name="envelope">The envelope to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SendAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken);
}
