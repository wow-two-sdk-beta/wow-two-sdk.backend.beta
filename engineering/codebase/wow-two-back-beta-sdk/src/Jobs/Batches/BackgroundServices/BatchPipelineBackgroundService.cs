using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Batches.BackgroundServices;

/// <summary>Runs a batch handler as pipeline items arrive, and drains accepted items during graceful shutdown.</summary>
/// <typeparam name="TItem">The work item type.</typeparam>
/// <remarks>
///   - each batch runs in its own scope; a failed batch is counted and logged, and the loop continues
///   - shutdown stops intake first, then drains until the host's shutdown budget expires
///   - items still queued when the budget expires are counted as failed
/// </remarks>
public sealed class BatchPipelineBackgroundService<TItem> : BackgroundService
{
    private readonly BoundedBatchPipeline<TItem> _pipeline;
    private readonly IServiceScopeFactory _scopes;
    private readonly BatchPipelineOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<BatchPipelineBackgroundService<TItem>> _logger;

    /// <summary>Creates the loop over one pipeline.</summary>
    /// <param name="pipeline">The pipeline to drain.</param>
    /// <param name="scopes">The factory that scopes each batch.</param>
    /// <param name="options">The batch size and delay.</param>
    /// <param name="clock">The clock that times a partial batch's wait.</param>
    /// <param name="logger">The logger for failed and abandoned batches.</param>
    public BatchPipelineBackgroundService(
        BoundedBatchPipeline<TItem> pipeline,
        IServiceScopeFactory scopes,
        BatchPipelineOptions options,
        TimeProvider clock,
        ILogger<BatchPipelineBackgroundService<TItem>> logger)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.BatchSize, 1, nameof(options));
        _pipeline = pipeline;
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _pipeline.Complete();
        if (ExecuteTask is not null)
        {
            try
            {
                await ExecuteTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The shutdown budget expired; the base stop cancels the batch in flight.
            }
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<TItem>(_options.BatchSize);
        try
        {
            while (await _pipeline.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                await FillAsync(batch, stoppingToken).ConfigureAwait(false);
                await FlushAsync(batch, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host's shutdown budget expired before the pipeline drained.
        }
        finally
        {
            _pipeline.Complete();
            long abandoned = batch.Count;
            while (_pipeline.Reader.TryRead(out _))
            {
                abandoned++;
            }

            if (abandoned > 0)
            {
                _pipeline.RecordFailed((int)Math.Min(abandoned, int.MaxValue));
                _logger.BatchItemsAbandoned(_pipeline.Name, abandoned);
            }
        }
    }

    private async Task FillAsync(List<TItem> batch, CancellationToken stoppingToken)
    {
        while (batch.Count < _options.BatchSize && _pipeline.Reader.TryRead(out TItem? item))
        {
            batch.Add(item);
        }

        if (batch.Count >= _options.BatchSize || _options.MaxBatchDelay <= TimeSpan.Zero)
        {
            return;
        }

        using var delay = new CancellationTokenSource(_options.MaxBatchDelay, _clock);
        using var linger = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, delay.Token);
        try
        {
            while (batch.Count < _options.BatchSize && await _pipeline.Reader.WaitToReadAsync(linger.Token).ConfigureAwait(false))
            {
                while (batch.Count < _options.BatchSize && _pipeline.Reader.TryRead(out TItem? item))
                {
                    batch.Add(item);
                }
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // The delay elapsed; flush the partial batch.
        }
    }

    private async Task FlushAsync(List<TItem> batch, CancellationToken stoppingToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        TItem[] items = [.. batch];
        try
        {
            AsyncServiceScope scope = _scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                IBatchHandler<TItem> handler = scope.ServiceProvider.GetRequiredService<IBatchHandler<TItem>>();
                await handler.HandleAsync(items, stoppingToken).ConfigureAwait(false);
            }

            _pipeline.RecordProcessed(items.Length);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _pipeline.RecordFailed(items.Length);
            batch.Clear();
            throw;
        }
        catch (Exception exception)
        {
            _pipeline.RecordFailed(items.Length);
            _logger.BatchFailed(exception, _pipeline.Name, items.Length);
        }

        batch.Clear();
    }
}
