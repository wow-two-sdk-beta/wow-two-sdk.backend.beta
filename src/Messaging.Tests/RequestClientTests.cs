using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>A request contract answered by <see cref="PriceRequestedHandler"/>.</summary>
public sealed record PriceRequested(string OrderId) : IEvent;

/// <summary>The response contract for <see cref="PriceRequested"/>.</summary>
public sealed record PriceQuoted(string OrderId, decimal Amount) : IEvent;

/// <summary>A request nothing ever answers — the timeout path.</summary>
public sealed record SilentRequested(string OrderId) : IEvent;

/// <summary>Responds through the documented responder-side helper.</summary>
public sealed class PriceRequestedHandler : IEventHandler<PriceRequested>
{
    public ValueTask HandleAsync(EventContext<PriceRequested> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.RespondAsync(new PriceQuoted(context.Event.OrderId, 42m), cancellationToken);
    }
}

/// <summary>Consumes a request and deliberately never replies.</summary>
public sealed class SilentRequestedHandler : IEventHandler<SilentRequested>
{
    public ValueTask HandleAsync(EventContext<SilentRequested> context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

/// <summary>
/// Exists so a <see cref="PriceQuoted"/> that reaches the pipeline dispatches to something. Nothing asserts on this
/// handler's behaviour — what matters is the <see cref="ConsumeOutcome"/>, which is <c>Success</c> only when a reply was
/// <i>not</i> intercepted by the request client's consume filter.
/// </summary>
public sealed class PriceQuotedHandler : IEventHandler<PriceQuoted>
{
    public ValueTask HandleAsync(EventContext<PriceQuoted> context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

/// <summary>Request/response over the shared bus: the round trip, the timeout, and that a timed-out request leaves nothing behind.</summary>
public sealed class RequestClientTests
{
    [Fact]
    public async Task Request_is_answered_by_the_responder()
    {
        await using var harness = await StartAsync();
        var client = harness.Services.GetRequiredService<IRequestClient<PriceRequested, PriceQuoted>>();

        var response = await client.GetResponseAsync(new PriceRequested("order-1"));

        response.OrderId.Should().Be("order-1");
        response.Amount.Should().Be(42m);

        var request = harness.Published.Of<PriceRequested>().Should().ContainSingle().Which.Envelope;
        request.ReplyTo.Should().NotBeNullOrEmpty(); // the client stamps the address, not the caller
        request.ConversationId.Should().NotBeNullOrEmpty();

        var reply = harness.Published.Of<PriceQuoted>().Should().ContainSingle().Which.Envelope;
        reply.ConversationId.Should().Be(request.ConversationId); // the pairing the client matches on
        reply.ReplyTo.Should().BeNull(); // deliberately not inherited — the response is one-way

        // Nothing to await for a delivery that never happens, so the assertion is that the bus fell silent with the
        // reply short-circuited: the requesting process needs no handler for its own responses.
        await harness.WaitForIdleAsync();
        harness.Consumed.Any<PriceQuoted>().Should().BeFalse();
    }

    [Fact]
    public async Task Request_times_out_when_nothing_responds()
    {
        var time = new FakeTimeProvider();
        await using var harness = await StartAsync(time);
        var client = harness.Services.GetRequiredService<IRequestClient<SilentRequested, PriceQuoted>>();

        var request = client
            .GetResponseAsync(new SilentRequested("order-2"), new RequestOptions { Timeout = TimeSpan.FromSeconds(30) })
            .AsTask();

        await harness.Consumed.WaitForAsync<SilentRequested>(); // delivered, and deliberately left unanswered
        await AdvanceUntilCompletedAsync(time, request);

        Func<Task> awaitRequest = () => request;
        var thrown = await awaitRequest.Should().ThrowAsync<RequestTimeoutException>();
        thrown.WithMessage("*PriceQuoted*SilentRequested*00:00:30*");
        thrown.Which.InnerException.Should().BeOfType<TimeoutException>();

        harness.Consumed.Count<SilentRequested>().Should().Be(1); // the request was delivered; only the reply never came
    }

    [Fact]
    public async Task Timed_out_request_releases_its_pending_entry()
    {
        var time = new FakeTimeProvider();
        await using var harness = await StartAsync(time);
        var client = harness.Services.GetRequiredService<IRequestClient<SilentRequested, PriceQuoted>>();

        var request = client.GetResponseAsync(new SilentRequested("order-3")).AsTask();
        await harness.Consumed.WaitForAsync<SilentRequested>();
        await AdvanceUntilCompletedAsync(time, request);

        Func<Task> awaitRequest = () => request;
        await awaitRequest.Should().ThrowAsync<RequestTimeoutException>();

        var conversationId = harness.Published.Of<SilentRequested>().Should().ContainSingle().Which.Envelope.ConversationId;
        conversationId.Should().NotBeNullOrEmpty();

        // The registry is internal, so the leak check is behavioural rather than a field read: a message on that
        // conversation is swallowed by the consume filter for exactly as long as an entry is registered for it. Its
        // reaching a handler is the proof the finally released the entry — the contrast is the round-trip test above,
        // where the same type is intercepted and never consumed.
        await harness.Bus.PublishAsync(
            new PriceQuoted("order-3", 7m),
            new PublishOptions { MessageId = "late-reply-1", ConversationId = conversationId });

        var consumed = await harness.Consumed.WaitForAsync<PriceQuoted>();
        consumed[0].MessageId.Should().Be("late-reply-1");
        consumed[0].Outcome.Should().Be(ConsumeOutcome.Success);
    }

    private static Task<MessagingTestHarness> StartAsync(TimeProvider? timeProvider = null)
        => MessagingTestHarness.StartAsync(
            services =>
            {
                // The bus registered TimeProvider.System with a TryAdd before this runs, so a fake needs a Replace.
                if (timeProvider is not null)
                    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(timeProvider));

                services.AddRequestClient();
            },
            handlerAssemblies: [typeof(PriceRequestedHandler).Assembly]);

    /// <summary>
    /// Push the fake clock forward until <paramref name="task"/> lands. A single <c>Advance</c> would race the client:
    /// the timer only exists once <c>GetResponseAsync</c> has published the request and reached its wait, and that
    /// continuation is not ordered against the test's own thread. Advancing until the condition holds is that ordering
    /// expressed as a condition instead of a guessed sleep; the wall-clock budget only bounds a genuine hang.
    /// </summary>
    private static async Task AdvanceUntilCompletedAsync(FakeTimeProvider time, Task task, TimeSpan? step = null, TimeSpan? budget = null)
    {
        var stride = step ?? TimeSpan.FromSeconds(1);
        var limit = budget ?? TimeSpan.FromSeconds(10);
        var started = Stopwatch.GetTimestamp();

        while (!task.IsCompleted)
        {
            if (Stopwatch.GetElapsedTime(started) > limit)
                throw new TimeoutException($"The request never completed after advancing the clock for {limit} of wall time.");

            time.Advance(stride);
            await Task.Yield();
        }
    }
}
