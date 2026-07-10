using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>In-memory <see cref="ISendTransport"/> — writes the envelope to the channel, deferring to the scheduler when it carries a future delivery time.</summary>
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
