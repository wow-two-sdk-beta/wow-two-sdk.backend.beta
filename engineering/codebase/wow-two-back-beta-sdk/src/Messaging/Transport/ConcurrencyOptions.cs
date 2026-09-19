using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Holds consume-side concurrency for every transport. A receive loop hands each message to the pump instead of awaiting the
/// pipeline inline, so the loop keeps pulling while handlers run on worker tasks.
/// </summary>
/// <remarks>
///   - defaults to 1 — dispatch stays inline and sequential on the consume loop
///   - raise <see cref="MaxConcurrentMessages"/> to consume in parallel
///   - keep the broker's prefetch window at least as large, or it caps concurrency instead
/// </remarks>
public sealed record ConcurrencyOptions
{
    /// <summary>
    /// Messages processed concurrently. <c>1</c> (default) dispatches inline on the consume loop. Values above 1 start
    /// that many worker tasks. Forced to 1 for a transport reporting <see cref="ITransportCapabilities.ThreadAffineConsume"/>.
    /// </summary>
    public int MaxConcurrentMessages { get; set; } = 1;

    /// <summary>
    /// Messages a single worker may have queued ahead of the one it is processing. The default <c>1</c> makes the pump
    /// apply backpressure to the consume loop as soon as every worker is busy, leaving unclaimed messages at the broker.
    /// </summary>
    public int MaxQueuedMessagesPerWorker { get; set; } = 1;

    /// <summary>
    /// Route messages sharing an <see cref="EventEnvelopeModel.PartitionKey"/> to the same worker, so they stay ordered
    /// relative to each other while unrelated keys run in parallel. Disable for maximum throughput when order is
    /// irrelevant. Messages with no partition key are distributed round-robin either way.
    /// </summary>
    public bool PreserveKeyOrder { get; set; } = true;

    /// <summary>How long shutdown draining waits for in-flight handlers before giving up and letting the host stop. Default 30s.</summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
