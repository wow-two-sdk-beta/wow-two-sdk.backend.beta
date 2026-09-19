using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>In-memory <see cref="ReceiveContext"/> — acknowledge is a no-op (the channel read already removed it); dead-letter writes to the <see cref="IDeadLetterRepository"/>.</summary>
internal sealed class InMemoryReceiveContext(EventEnvelopeModel envelope, IDeadLetterRepository deadLetters, TimeProvider timeProvider) : ReceiveContext
{
    public override EventEnvelopeModel Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        var record = new DeadLetterRecord
        {
            MessageId = envelope.MessageId,
            Destination = envelope.Destination,
            Reason = reason,
            ExceptionType = exception?.GetType().FullName,
            Envelope = envelope,
            DeadLetteredAtUtc = timeProvider.GetUtcNow(),
        };
        return deadLetters.DeadLetterAsync(record, cancellationToken);
    }
}
