namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Defines behavior that drains staged outbox messages to the transport. Driven by a hosted service or the backing engine.</summary>
public interface IOutboxDispatcher
{
    /// <summary>Dispatch up to <paramref name="batchSize"/> pending outbox messages; returns the number dispatched.</summary>
    /// <param name="batchSize">Maximum messages to dispatch in this pass.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> DispatchAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>Prune processed (dispatched or given-up) rows older than <paramref name="retention"/>; returns rows removed. No-op by default.</summary>
    /// <param name="retention">How long to retain processed rows before pruning.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> PruneProcessedAsync(TimeSpan retention, CancellationToken cancellationToken) => ValueTask.FromResult(0);
}
