using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Covers concurrent inbox retries, deduplication and independent message progress.</summary>
public sealed class InMemoryInboxConcurrencyTests
{
    [Fact]
    public async Task Failed_attempts_can_release_and_reacquire_the_same_message_concurrently()
    {
        await using var services = new ServiceCollection().AddInMemoryReliability().BuildServiceProvider();
        var inbox = services.GetRequiredService<IInboxProcessor>();
        var calls = 0;
        const int attempts = 20_000;

        await Parallel.ForAsync(0, attempts, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (_, ct) =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await inbox.ProcessOnceAsync("retry", _ =>
                {
                    Interlocked.Increment(ref calls);
                    throw new InvalidOperationException("Retryable handler failure.");
                }, ct));
        });

        calls.Should().Be(attempts);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            inbox.ProcessOnceAsync("retry", _ => ValueTask.CompletedTask, CancellationToken.None).AsTask()));
        outcomes.Count(processed => processed).Should().Be(1);
    }

    [Fact]
    public async Task Waiting_duplicate_cancellation_does_not_block_another_message()
    {
        await using var services = new ServiceCollection().AddInMemoryReliability().BuildServiceProvider();
        var inbox = services.GetRequiredService<IInboxProcessor>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = inbox.ProcessOnceAsync("first", _ => new ValueTask(release.Task), CancellationToken.None).AsTask();
        using var cancellation = new CancellationTokenSource();

        try
        {
            var waiting = inbox.ProcessOnceAsync("first", _ => ValueTask.CompletedTask, cancellation.Token).AsTask();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

            var second = await inbox.ProcessOnceAsync("second", _ => ValueTask.CompletedTask, CancellationToken.None)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            second.Should().BeTrue();
        }
        finally
        {
            release.TrySetResult();
            await first;
        }

        var duplicate = await inbox.ProcessOnceAsync("first", _ => ValueTask.CompletedTask, CancellationToken.None);
        duplicate.Should().BeFalse();
    }
}
