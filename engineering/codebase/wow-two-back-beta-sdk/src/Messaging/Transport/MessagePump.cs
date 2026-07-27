using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Consume-side concurrency for every transport. A receive loop hands each message to the pump instead of awaiting the
/// pipeline inline, so the loop keeps pulling while handlers run on worker tasks.
/// </summary>
/// <remarks>
/// The default (<see cref="MaxConcurrentMessages"/> = 1) is behaviour-preserving: dispatch stays inline and sequential,
/// exactly as before the pump existed. Raise it to consume in parallel; a broker's own prefetch/fetch window should be
/// at least as large or it becomes the limit instead.
/// </remarks>
public sealed class ConcurrencyOptions
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
    /// Route messages sharing an <see cref="EventEnvelope.PartitionKey"/> to the same worker, so they stay ordered
    /// relative to each other while unrelated keys run in parallel. Disable for maximum throughput when order is
    /// irrelevant. Messages with no partition key are distributed round-robin either way.
    /// </summary>
    public bool PreserveKeyOrder { get; set; } = true;

    /// <summary>How long shutdown draining waits for in-flight handlers before giving up and letting the host stop. Default 30s.</summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Sits between an <see cref="IReceiveTransport"/> and the <see cref="EventProcessingPipeline"/>, turning a sequential
/// receive loop into N workers. Tracks in-flight count for graceful drain (and, later, the in-flight gauge and bus control).
/// </summary>
internal sealed partial class MessagePump : IAsyncDisposable
{
    private readonly EventProcessingPipeline _pipeline;
    private readonly ILogger<MessagePump> _logger;
    private readonly ConcurrencyOptions _options;
    private readonly Channel<ReceiveContext>[] _workers;
    private readonly Task[] _workerLoops;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly bool _sequential;
    private readonly IDisposable _inFlightGauge;
    private readonly Lock _idleSync = new();
    private TaskCompletionSource? _idleSignal;
    private int _inFlight;
    private uint _nextWorker;

    public MessagePump(
        EventProcessingPipeline pipeline,
        IOptions<ConcurrencyOptions> options,
        IEnumerable<ITransportCapabilities> capabilities,
        IMessagingMetrics metrics,
        ILogger<MessagePump> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(metrics);
        _pipeline = pipeline;
        _logger = logger;
        _options = options.Value;

        // The gauge pulls; the pump never pushes. Registered before the sequential early-return because _inFlight is
        // maintained in both modes.
        _inFlightGauge = metrics.TrackInFlight(() => InFlight);

        var requested = Math.Max(1, _options.MaxConcurrentMessages);
        var threadAffine = capabilities.FirstOrDefault()?.ThreadAffineConsume ?? false;
        if (threadAffine && requested > 1)
        {
            // Parallelising here would settle (advance offsets) off the consume thread — the one thing the transport forbids.
            LogThreadAffineDegraded(requested);
            requested = 1;
        }

        _sequential = requested == 1;
        if (_sequential)
        {
            _workers = [];
            _workerLoops = [];
            return;
        }

        var capacity = Math.Max(1, _options.MaxQueuedMessagesPerWorker);
        _workers = new Channel<ReceiveContext>[requested];
        _workerLoops = new Task[requested];
        for (var i = 0; i < requested; i++)
        {
            // FullMode.Wait is the backpressure: a busy worker blocks the dispatching consume loop rather than buffering.
            var channel = Channel.CreateBounded<ReceiveContext>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

            _workers[i] = channel;
            _workerLoops[i] = Task.Run(() => RunWorkerAsync(channel.Reader), CancellationToken.None);
        }

        LogPumpStarted(requested, capacity, _options.PreserveKeyOrder);
    }

    /// <summary>Messages currently being processed (queued to a worker or executing). Messages parked at <see cref="Gate"/> are not counted — they never entered the pipeline.</summary>
    public int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>
    /// The pump's entry gate, driven by <see cref="IBusControl"/>. Open by default; shut, it parks the consume loop at
    /// <see cref="DispatchAsync"/> so unconsumed messages stay unsettled at the broker and prefetch backpressures.
    /// </summary>
    public AsyncGate Gate { get; } = new();

    /// <summary>
    /// The callback handed to <see cref="IReceiveTransport.StartAsync"/>. Sequential mode awaits the pipeline inline;
    /// parallel mode returns once a worker has accepted the message, which is what lets the loop keep pulling.
    /// </summary>
    public async ValueTask DispatchAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Pause gates ENTRY, and sits upstream of both modes and of the in-flight counter. Two consequences, both
        // deliberate: a message already admitted still completes (pause never abandons work), and a parked message is
        // invisible to InFlight — so a drain against a paused pump reaches zero instead of waiting on gated work.
        // The token is the transport's, so host shutdown unwinds a parked loop rather than deadlocking on the gate.
        await Gate.WaitAsync(cancellationToken);

        if (_sequential)
        {
            Interlocked.Increment(ref _inFlight);
            try
            {
                await _pipeline.ProcessAsync(context, cancellationToken);
            }
            finally
            {
                CompleteOne();
            }

            return;
        }

        var worker = _workers[SelectWorker(context.Envelope.PartitionKey)];
        Interlocked.Increment(ref _inFlight);
        try
        {
            await worker.Writer.WriteAsync(context, cancellationToken);
        }
        catch
        {
            CompleteOne(); // never accepted — the worker will not decrement it
            throw;
        }
    }

    /// <summary>
    /// Wait until nothing is in flight. Complements <see cref="DrainAsync"/>: that one retires the worker loops (and is
    /// inert in sequential mode, where there are none), this one covers work executing inline on the consume loop.
    /// </summary>
    /// <remarks>
    /// Only terminates if arrivals have been stopped first — shutting <see cref="Gate"/>, cancelling the consume loop,
    /// or both. Signal-based, never polled: the last completing message hands the waiter its result.
    /// </remarks>
    /// <param name="cancellationToken">Bounds the wait — an overrunning handler must not hang a caller forever.</param>
    public Task WaitForIdleAsync(CancellationToken cancellationToken)
    {
        Task idle;
        lock (_idleSync)
        {
            // Read under the lock that CompleteOne signals under: a decrement landing between this read and the
            // registration below either already drove the count to zero (seen here) or wakes the source it just saw
            // published. Never both missed.
            if (Volatile.Read(ref _inFlight) == 0)
                return Task.CompletedTask;

            _idleSignal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            idle = _idleSignal.Task;
        }

        return idle.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Stop accepting work and wait for in-flight handlers, bounded by <see cref="ConcurrencyOptions.DrainTimeout"/>.
    /// Called after the consume loop stops and before the transport is released, so settlement still has a live channel.
    /// </summary>
    /// <remarks>
    /// Safe to call on a paused pump. <see cref="Gate"/> sits in front of <see cref="DispatchAsync"/>, not inside the
    /// worker loop, so a shut gate cannot hold a worker back from finishing what it already accepted — draining a
    /// paused pump retires exactly the admitted work and returns. Terminal: the worker channels are completed, so a
    /// drained pump does not accept messages again.
    /// </remarks>
    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        if (_sequential)
            return;

        foreach (var worker in _workers)
            worker.Writer.TryComplete();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.DrainTimeout);
        try
        {
            await Task.WhenAll(_workerLoops).WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            LogDrainTimedOut(InFlight, _options.DrainTimeout);
        }
    }

    private async Task RunWorkerAsync(ChannelReader<ReceiveContext> reader)
    {
        try
        {
            // Deliberately NOT the transport's stopping token: that is already cancelled by the time draining starts, so
            // handing it to the handler would abandon exactly the in-flight work the drain exists to finish. _shutdown
            // outlives the drain and is cancelled only on dispose, which is what bounds a handler that overruns.
            await foreach (var context in reader.ReadAllAsync(_shutdown.Token))
            {
                try
                {
                    // ProcessAsync owns its own failure path (retry → dead-letter); a throw here is the pipeline itself
                    // faulting, and must not kill the worker and silently stop this partition.
                    await _pipeline.ProcessAsync(context, _shutdown.Token);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    // shutting down
                }
                catch (Exception ex)
                {
                    LogWorkerFaulted(ex, context.Envelope.MessageId);
                }
                finally
                {
                    CompleteOne();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    // Every in-flight decrement funnels through here so no site can miss the idle signal.
    //
    // Do NOT add a lock-free `if (_idleSignal is null) return;` fast path: WaitForIdleAsync reads _inFlight and
    // publishes its source inside _idleSync, and only that mutual exclusion rules out the lost wakeup where the waiter
    // reads a stale non-zero count AND this method reads a not-yet-published source. Uncontended, the lock is noise
    // next to the deserialize + dispatch + settle the message just paid for.
    private void CompleteOne()
    {
        if (Interlocked.Decrement(ref _inFlight) != 0)
            return;

        TaskCompletionSource? idle;
        lock (_idleSync)
        {
            idle = _idleSignal;
            _idleSignal = null; // cleared so a later wait registers a fresh source instead of a completed one
        }

        idle?.TrySetResult(); // outside the lock — continuations must never run under it
    }

    // Same key → same worker → ordered within the key. No key (or ordering disabled) → round-robin across workers.
    private int SelectWorker(string? partitionKey)
    {
        if (_options.PreserveKeyOrder && !string.IsNullOrEmpty(partitionKey))
            return (int)(StableHash(partitionKey) % (uint)_workers.Length);

        return (int)(Interlocked.Increment(ref _nextWorker) % (uint)_workers.Length);
    }

    // FNV-1a: string.GetHashCode is randomised per process, and reproducible routing keeps tests deterministic.
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash;
    }

    public async ValueTask DisposeAsync()
    {
        _inFlightGauge.Dispose(); // stop the gauge reading a pump that is tearing down

        foreach (var worker in _workers)
            worker.Writer.TryComplete();

        await _shutdown.CancelAsync();
        _shutdown.Dispose();
    }

    [LoggerMessage(EventId = 6021, Level = LogLevel.Information, Message = "Message pump started with {WorkerCount} workers (queue {QueueCapacity} per worker, key ordering {PreserveKeyOrder})")]
    private partial void LogPumpStarted(int workerCount, int queueCapacity, bool preserveKeyOrder);

    [LoggerMessage(EventId = 6022, Level = LogLevel.Warning, Message = "Transport requires thread-affine consume; MaxConcurrentMessages={Requested} ignored and dispatch stays sequential")]
    private partial void LogThreadAffineDegraded(int requested);

    [LoggerMessage(EventId = 6023, Level = LogLevel.Error, Message = "Message pump worker faulted processing {MessageId}")]
    private partial void LogWorkerFaulted(Exception exception, string messageId);

    [LoggerMessage(EventId = 6024, Level = LogLevel.Warning, Message = "Drain timed out after {DrainTimeout} with {InFlight} messages still in flight")]
    private partial void LogDrainTimedOut(int inFlight, TimeSpan drainTimeout);
}
