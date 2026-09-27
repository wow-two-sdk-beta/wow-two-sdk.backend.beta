namespace WoW.Two.Sdk.Backend.Beta.Jobs.Batches;

/// <summary>Defines handling one batch of work items inside its own dependency-injection scope.</summary>
/// <typeparam name="TItem">The work item type.</typeparam>
public interface IBatchHandler<in TItem>
{
    /// <summary>Handles <paramref name="batch"/>; a thrown exception marks the whole batch failed.</summary>
    /// <param name="batch">The items, in acceptance order.</param>
    /// <param name="cancellationToken">Cancels the batch when the host's shutdown budget expires.</param>
    Task HandleAsync(IReadOnlyList<TItem> batch, CancellationToken cancellationToken);
}
