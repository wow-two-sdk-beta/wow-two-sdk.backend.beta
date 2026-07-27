using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>The in-memory bus end to end: round-trip, dedupe, dead-letter — driven through the shipped harness.</summary>
public sealed class InMemoryEventBusTests
{
    private static Task<MessagingTestHarness> StartAsync(Action<InMemoryEventBusOptions>? configureBus = null)
        => MessagingTestHarness.StartAsync(
            static services => services.AddSingleton<EventCollector>(), // PingHandler's collaborator, still shared with the broker suites
            configureBus,
            [typeof(PingHandler).Assembly]);

    [Fact]
    public async Task Publishes_and_handles_event()
    {
        await using var harness = await StartAsync();

        await harness.Bus.PublishAsync(new PingEvent("hello"));

        await harness.Consumed.WaitForAsync<PingEvent>();
        harness.Consumed.Count<PingEvent>().Should().Be(1);
        harness.Published.Count<PingEvent>(e => e.Value == "hello").Should().Be(1); // the send half, which a handler-side collector cannot see
    }

    [Fact]
    public async Task Deduplicates_by_message_id()
    {
        await using var harness = await StartAsync();

        await harness.Bus.PublishAsync(new PingEvent("a"), new PublishOptions { MessageId = "dup-1" });
        await harness.Bus.PublishAsync(new PingEvent("b"), new PublishOptions { MessageId = "dup-1" });

        // Both deliveries have to be seen before the counts mean anything, and the second one produces no handler call
        // to await — so wait for the bus to fall silent instead of sleeping a guessed 300ms.
        await harness.WaitForIdleAsync();

        harness.Published.Count<PingEvent>().Should().Be(2);
        harness.Consumed.Count(m => m.Is<PingEvent>() && m.Outcome == ConsumeOutcome.Success).Should().Be(1);
        harness.Consumed.Count(m => m.Is<PingEvent>() && m.Outcome == ConsumeOutcome.Duplicate).Should().Be(1); // skipped by the inbox, not lost in transit
    }

    [Fact]
    public async Task Dead_letters_after_retries_exhausted()
    {
        await using var harness = await StartAsync(o => o.Retry = new RetryConfig(MaxAttempts: 2, Backoff: BackoffKind.None));

        await harness.Bus.PublishAsync(new BoomEvent("x"), new PublishOptions { MessageId = "boom-1" });

        var deadLettered = await harness.DeadLettered.WaitForAsync<BoomEvent>();
        deadLettered[0].Exception.Should().BeOfType<InvalidOperationException>(); // terminal exception captured, not null
        harness.Faulted.Count<BoomEvent>().Should().Be(2); // MaxAttempts = 2 — the budget was spent, not short-circuited

        // The dead-letter hook fires after settlement, so the store is already written: read it once, no polling loop.
        var record = await FirstDeadLetterAsync(harness, source: nameof(BoomEvent), messageId: "boom-1");
        record.Should().NotBeNull();
        record!.ExceptionType.Should().Contain(nameof(InvalidOperationException));
    }

    private static async Task<DeadLetterRecord?> FirstDeadLetterAsync(MessagingTestHarness harness, string source, string messageId)
    {
        var store = harness.Services.GetRequiredService<IDeadLetterStore>();
        await foreach (var record in store.ReadAsync(source, CancellationToken.None))
            if (record.MessageId == messageId)
                return record;

        return null;
    }
}
