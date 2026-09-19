namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Defines the receive half of a transport — subscribes and drives each received message into the processing pipeline.
/// Started/stopped by a hosted service; the callback is the transport-agnostic <c>EventProcessingPipeline</c>.
/// </summary>
public interface IReceiveTransport
{
    /// <summary>Begin receiving; invoke <paramref name="onMessage"/> for each message until stopped.</summary>
    /// <param name="onMessage">The processing callback (dedupe → resilience → dispatch → settle).</param>
    /// <param name="cancellationToken">Cancellation token tied to host lifetime.</param>
    ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken);

    /// <summary>Stop receiving and release transport resources.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask StopAsync(CancellationToken cancellationToken);
}
