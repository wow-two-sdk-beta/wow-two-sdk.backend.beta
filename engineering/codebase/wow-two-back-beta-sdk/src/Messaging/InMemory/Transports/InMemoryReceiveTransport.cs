using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory.Transports;

/// <summary>Transports envelopes from the in-memory channel into the pipeline until the host stops.</summary>
internal sealed class InMemoryReceiveTransport(InMemoryEventChannel channel, IDeadLetterRepository deadLetters, TimeProvider timeProvider) : IReceiveTransport
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
