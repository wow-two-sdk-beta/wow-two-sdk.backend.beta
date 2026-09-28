namespace WoW.Two.Sdk.Backend.Beta.Jobs.Batches;

/// <summary>Defines accepting work items that a background loop hands to a handler in batches.</summary>
/// <typeparam name="TItem">The work item type.</typeparam>
/// <remarks>Best-effort and in-memory: accepted items are lost if the process dies before their batch commits.</remarks>
public interface IBatchPipeline<in TItem>
{
    /// <summary>Accepts <paramref name="item"/> for a coming batch without waiting.</summary>
    /// <param name="item">The work item.</param>
    /// <returns><see langword="false"/> when the pipeline is full or stopping; the item is dropped and counted.</returns>
    bool TryWrite(TItem item);

    /// <summary>Accepts <paramref name="item"/>, waiting while the pipeline is full — backpressure instead of loss.</summary>
    /// <param name="item">The work item.</param>
    /// <param name="cancellationToken">Stops waiting for capacity.</param>
    /// <returns><see langword="false"/> when the pipeline is stopping; the item is dropped and counted.</returns>
    /// <exception cref="OperationCanceledException">The caller gave up waiting; the item was not accepted.</exception>
    ValueTask<bool> WriteAsync(TItem item, CancellationToken cancellationToken = default);
}
