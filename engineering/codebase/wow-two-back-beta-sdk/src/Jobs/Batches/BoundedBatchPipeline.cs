using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Batches;

/// <summary>Buffers work items in a bounded channel and counts every dropped, processed and failed item.</summary>
/// <typeparam name="TItem">The work item type.</typeparam>
/// <remarks>
/// <see cref="TryWrite"/> never waits: a full or stopping pipeline drops the item, counts it and logs at powers of two.
/// <see cref="WriteAsync"/> waits for capacity instead and drops only once the pipeline is stopping.
/// </remarks>
public sealed class BoundedBatchPipeline<TItem> : IBatchPipeline<TItem>
{
    private static readonly Meter Meter = new("WoW.Two.Sdk.Backend.Beta.Jobs.Batches");
    private static readonly Counter<long> Dropped = Meter.CreateCounter<long>("batch.items.dropped");
    private static readonly Counter<long> Processed = Meter.CreateCounter<long>("batch.items.processed");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("batch.items.failed");

    private readonly Channel<TItem> _channel;
    private readonly KeyValuePair<string, object?> _tag;
    private readonly ILogger<BoundedBatchPipeline<TItem>> _logger;
    private long _droppedCount;
    private long _processedCount;
    private long _failedCount;

    /// <summary>Creates the pipeline with the configured capacity.</summary>
    /// <param name="options">The pipeline name and capacity.</param>
    /// <param name="logger">The logger for dropped items.</param>
    public BoundedBatchPipeline(BatchPipelineOptions options, ILogger<BoundedBatchPipeline<TItem>> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, 1, nameof(options));
        Name = string.IsNullOrWhiteSpace(options.Name) ? typeof(TItem).Name : options.Name;
        _tag = new KeyValuePair<string, object?>("pipeline", Name);
        _logger = logger;
        _channel = Channel.CreateBounded<TItem>(new BoundedChannelOptions(options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Gets the name that tags this pipeline's metrics and logs.</summary>
    public string Name { get; }

    /// <summary>Gets the items rejected because the pipeline was full or stopping.</summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>Gets the items whose batch handler completed.</summary>
    public long ProcessedCount => Interlocked.Read(ref _processedCount);

    /// <summary>Gets the accepted items whose batch failed or never ran before shutdown.</summary>
    public long FailedCount => Interlocked.Read(ref _failedCount);

    internal ChannelReader<TItem> Reader => _channel.Reader;

    /// <inheritdoc />
    public bool TryWrite(TItem item)
    {
        if (_channel.Writer.TryWrite(item))
        {
            return true;
        }

        RecordDropped();
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<bool> WriteAsync(TItem item, CancellationToken cancellationToken = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ChannelClosedException)
        {
            RecordDropped();
            return false;
        }
    }

    internal void Complete() => _channel.Writer.TryComplete();

    private void RecordDropped()
    {
        Dropped.Add(1, _tag);
        long count = Interlocked.Increment(ref _droppedCount);
        if ((count & (count - 1)) == 0)
        {
            _logger.BatchItemsDropped(Name, count);
        }
    }

    internal void RecordProcessed(int count)
    {
        Interlocked.Add(ref _processedCount, count);
        Processed.Add(count, _tag);
    }

    internal void RecordFailed(int count)
    {
        Interlocked.Add(ref _failedCount, count);
        Failed.Add(count, _tag);
    }
}
