using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

/// <summary>
/// <see cref="IPublisher.PublishAsync{T}"/> fan-out semantics: 0 / 1 / N handlers, sequential execution in
/// registration order, and a throwing handler aborting the rest of the chain.
/// </summary>
public sealed class NotificationTests
{
    private sealed record Signal(string Payload) : INotification;

    // A notification type with NO registered handlers — publishing must be a no-op (not throw).
    private sealed record Unhandled : INotification;

    private sealed class RecordingHandler(List<string> sink, string id) : INotificationHandler<Signal>
    {
        public ValueTask HandleAsync(Signal notification, CancellationToken cancellationToken)
        {
            sink.Add($"{id}:{notification.Payload}");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingHandler(List<string> sink, string id) : INotificationHandler<Signal>
    {
        public ValueTask HandleAsync(Signal notification, CancellationToken cancellationToken)
        {
            sink.Add($"{id}:throwing");
            throw new InvalidOperationException($"handler {id} failed");
        }
    }

    // Build a publisher with an explicit, ordered handler list (bypasses assembly scan so ordering is deterministic).
    private static (IPublisher Publisher, List<string> Sink) BuildPublisher(
        Func<List<string>, IEnumerable<INotificationHandler<Signal>>> handlers)
    {
        var sink = new List<string>();
        var services = new ServiceCollection().AddMediator(typeof(NotificationTests).Assembly);

        // Clear any scanned Signal handlers, then add the supplied ones in order.
        var existing = services.Where(d => d.ServiceType == typeof(INotificationHandler<Signal>)).ToList();
        foreach (var d in existing) services.Remove(d);
        foreach (var h in handlers(sink)) services.AddSingleton(h);

        var publisher = services.BuildServiceProvider().GetRequiredService<IPublisher>();
        return (publisher, sink);
    }

    [Fact]
    public async Task PublishAsync_ShouldBeNoop_WhenZeroHandlers()
    {
        var publisher = new ServiceCollection()
            .AddMediator(typeof(NotificationTests).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<IPublisher>();

        var act = async () => await publisher.PublishAsync(new Unhandled());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeHandler_WhenOneHandler()
    {
        var (publisher, sink) = BuildPublisher(s => [new RecordingHandler(s, "a")]);

        await publisher.PublishAsync(new Signal("ping"));

        sink.Should().ContainSingle().Which.Should().Be("a:ping");
    }

    [Fact]
    public async Task PublishAsync_ShouldInvokeAllInRegistrationOrder_WhenManyHandlers()
    {
        var (publisher, sink) = BuildPublisher(s =>
        [
            new RecordingHandler(s, "first"),
            new RecordingHandler(s, "second"),
            new RecordingHandler(s, "third"),
        ]);

        await publisher.PublishAsync(new Signal("x"));

        // Sequential, in the exact order registered.
        sink.Should().Equal("first:x", "second:x", "third:x");
    }

    [Fact]
    public async Task PublishAsync_ShouldAbortRemainingHandlers_WhenOneThrows()
    {
        var (publisher, sink) = BuildPublisher(s =>
        [
            new RecordingHandler(s, "before"),
            new ThrowingHandler(s, "boom"),
            new RecordingHandler(s, "after"), // must NOT run
        ]);

        var act = async () => await publisher.PublishAsync(new Signal("y"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*boom*");
        // The handler after the throwing one never ran — fan-out is sequential and abort-on-throw.
        sink.Should().Equal("before:y", "boom:throwing");
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenNotificationNull()
    {
        var publisher = new ServiceCollection()
            .AddMediator(typeof(NotificationTests).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<IPublisher>();

        var act = async () => await publisher.PublishAsync<Signal>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
