using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Jobs.Batches;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Jobs.Batches;

/// <summary>Batch pipelines deliver bounded batches, drop without blocking, survive a failed batch and drain on shutdown.</summary>
public sealed class BatchPipelineTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Items_ReachTheHandlerInBoundedBatches()
    {
        var probe = new HandlerProbe();
        using IHost host = await StartAsync(probe, options => options.BatchSize = 3);
        var pipeline = host.Services.GetRequiredService<IBatchPipeline<int>>();

        for (int item = 1; item <= 7; item++)
        {
            pipeline.TryWrite(item).Should().BeTrue();
        }

        await WaitUntilAsync(() => Counts(host).Processed == 7);
        probe.Batches.Should().OnlyContain(batch => batch.Count <= 3);
        probe.Batches.SelectMany(batch => batch).Should().Equal(1, 2, 3, 4, 5, 6, 7);
    }

    [Fact]
    public async Task FullPipeline_DropsWithoutBlockingTheProducer()
    {
        var probe = new HandlerProbe { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using IHost host = await StartAsync(probe, options => options.Capacity = 2);
        var pipeline = host.Services.GetRequiredService<IBatchPipeline<int>>();

        pipeline.TryWrite(1).Should().BeTrue();
        await probe.Started.Task.WaitAsync(Patience);
        pipeline.TryWrite(2).Should().BeTrue();
        pipeline.TryWrite(3).Should().BeTrue();
        pipeline.TryWrite(4).Should().BeFalse();

        Counts(host).Dropped.Should().Be(1);
        probe.Gate.SetResult();
        await WaitUntilAsync(() => Counts(host).Processed == 3);
    }

    [Fact]
    public async Task WriteAsync_WaitsForCapacityInsteadOfDropping()
    {
        var probe = new HandlerProbe { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using IHost host = await StartAsync(probe, options => options.Capacity = 1);
        var pipeline = host.Services.GetRequiredService<IBatchPipeline<int>>();

        (await pipeline.WriteAsync(1)).Should().BeTrue();
        await probe.Started.Task.WaitAsync(Patience);
        (await pipeline.WriteAsync(2)).Should().BeTrue();
        var waiting = pipeline.WriteAsync(3).AsTask();

        await Task.Delay(100);
        waiting.IsCompleted.Should().BeFalse();
        probe.Gate.SetResult();

        (await waiting.WaitAsync(Patience)).Should().BeTrue();
        await WaitUntilAsync(() => Counts(host).Processed == 3);
        Counts(host).Dropped.Should().Be(0);
    }

    [Fact]
    public async Task WriteAsync_HonoursCallerCancellationWhileFull()
    {
        var probe = new HandlerProbe { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using IHost host = await StartAsync(probe, options => options.Capacity = 1);
        var pipeline = host.Services.GetRequiredService<IBatchPipeline<int>>();
        await pipeline.WriteAsync(1);
        await probe.Started.Task.WaitAsync(Patience);
        await pipeline.WriteAsync(2);

        using var giveUp = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var act = async () => await pipeline.WriteAsync(3, giveUp.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        probe.Gate.SetResult();
        await WaitUntilAsync(() => Counts(host).Processed == 2);
    }

    [Fact]
    public async Task FailedBatch_IsCountedAndTheLoopContinues()
    {
        var probe = new HandlerProbe { FailFirstBatch = true };
        using IHost host = await StartAsync(probe, options => options.BatchSize = 1);
        var pipeline = host.Services.GetRequiredService<IBatchPipeline<int>>();

        pipeline.TryWrite(1);
        await WaitUntilAsync(() => Counts(host).Failed == 1);
        pipeline.TryWrite(2);

        await WaitUntilAsync(() => Counts(host).Processed == 1);
        Counts(host).Failed.Should().Be(1);
    }

    [Fact]
    public async Task Shutdown_DrainsAcceptedItemsAndRefusesNewOnes()
    {
        var probe = new HandlerProbe { Delay = TimeSpan.FromMilliseconds(20) };
        using IHost host = await StartAsync(probe, options => options.BatchSize = 1);
        var pipeline = host.Services.GetRequiredService<IBatchPipeline<int>>();
        for (int item = 1; item <= 5; item++)
        {
            pipeline.TryWrite(item);
        }

        using var budget = new CancellationTokenSource(Patience);
        await host.StopAsync(budget.Token);

        Counts(host).Processed.Should().Be(5);
        pipeline.TryWrite(6).Should().BeFalse();
    }

    [Fact]
    public async Task ShutdownBudgetExpiry_CountsUnhandledItemsAsFailed()
    {
        var probe = new HandlerProbe { WaitForCancellation = true };
        using IHost host = await StartAsync(probe, options => options.BatchSize = 1);
        var pipeline = host.Services.GetRequiredService<IBatchPipeline<int>>();
        for (int item = 1; item <= 3; item++)
        {
            pipeline.TryWrite(item);
        }

        await probe.Started.Task.WaitAsync(Patience);
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await host.StopAsync(budget.Token);

        // The expired budget cancels the batch in flight; the loop counts the rest as it exits.
        await WaitUntilAsync(() => Counts(host).Failed == 3);
        Counts(host).Processed.Should().Be(0);
    }

    private static async Task<IHost> StartAsync(HandlerProbe probe, Action<BatchPipelineOptions> configure)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddBatchPipeline<int, ProbeHandler>(configure);
        IHost host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static (long Processed, long Failed, long Dropped) Counts(IHost host)
    {
        var pipeline = host.Services.GetRequiredService<BoundedBatchPipeline<int>>();
        return (pipeline.ProcessedCount, pipeline.FailedCount, pipeline.DroppedCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the pipeline should reach the expected state");
            await Task.Delay(10);
        }
    }

    private sealed class HandlerProbe
    {
        public ConcurrentQueue<IReadOnlyList<int>> Batches { get; } = new();

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource? Gate { get; init; }

        public bool FailFirstBatch { get; init; }

        public bool WaitForCancellation { get; init; }

        public TimeSpan Delay { get; init; }

        public int Calls;
    }

    private sealed class ProbeHandler(HandlerProbe probe) : IBatchHandler<int>
    {
        public async Task HandleAsync(IReadOnlyList<int> batch, CancellationToken cancellationToken)
        {
            probe.Started.TrySetResult();
            if (Interlocked.Increment(ref probe.Calls) == 1 && probe.FailFirstBatch)
            {
                throw new InvalidOperationException("first batch fails");
            }

            if (probe.Gate is not null)
            {
                await probe.Gate.Task.WaitAsync(cancellationToken);
            }

            if (probe.WaitForCancellation)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            await Task.Delay(probe.Delay, cancellationToken);
            probe.Batches.Enqueue(batch);
        }
    }
}
