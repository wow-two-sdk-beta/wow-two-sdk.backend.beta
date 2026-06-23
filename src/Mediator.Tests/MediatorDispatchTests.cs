using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

/// <summary>
/// Core dispatch over the primitive <see cref="IRequest{TResponse}"/> / <see cref="IRequestHandler{TRequest,TResponse}"/>
/// surface (no CQRS markers) — sync-completing vs genuinely-async handlers, void requests, and the
/// <see cref="IMediator"/> single-instance resolving both sender + publisher roles.
/// </summary>
public sealed class MediatorDispatchTests
{
    // --- a request whose handler completes synchronously (ValueTask.FromResult — no state machine) ---

    private sealed record SyncQuery(int N) : IRequest<int>;

    private sealed class SyncQueryHandler : IRequestHandler<SyncQuery, int>
    {
        public ValueTask<int> HandleAsync(SyncQuery request, CancellationToken cancellationToken)
            => ValueTask.FromResult(request.N * 2); // completed synchronously
    }

    // --- a request whose handler genuinely yields (Task.Yield → async continuation) ---

    private sealed record AsyncQuery(int N) : IRequest<int>;

    private sealed class AsyncQueryHandler : IRequestHandler<AsyncQuery, int>
    {
        public async ValueTask<int> HandleAsync(AsyncQuery request, CancellationToken cancellationToken)
        {
            await Task.Yield(); // forces an actual asynchronous hop
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return request.N + 100;
        }
    }

    // --- a void request via the primitive IRequestHandler<TRequest> (= IRequestHandler<TRequest, Unit>) ---

    private sealed record DoThing(string Tag) : IRequest;

    private sealed class DoThingHandler : IRequestHandler<DoThing>
    {
        public static string? LastTag;

        public ValueTask<Unit> HandleAsync(DoThing request, CancellationToken cancellationToken)
        {
            LastTag = request.Tag;
            return Unit.ValueTask;
        }
    }

    private static IMediator BuildMediator()
        => new ServiceCollection()
            .AddMediator(typeof(MediatorDispatchTests).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<IMediator>();

    [Fact]
    public async Task SendAsync_ShouldRunSynchronouslyCompletingHandler()
    {
        var mediator = BuildMediator();

        var result = await mediator.SendAsync(new SyncQuery(21));

        result.Should().Be(42);
    }

    [Fact]
    public async Task SendAsync_ShouldCompleteSynchronously_WhenSyncHandler()
    {
        var mediator = BuildMediator();

        // No behaviors registered → the pipeline head is the handler's own ValueTask, which is already completed.
        var pending = mediator.SendAsync(new SyncQuery(5));

        pending.IsCompleted.Should().BeTrue("a sync-completing handler with no behaviors should not allocate an async hop");
        (await pending).Should().Be(10); // already completed → reads synchronously
    }

    [Fact]
    public async Task SendAsync_ShouldRunGenuinelyAsyncHandler()
    {
        var mediator = BuildMediator();

        var result = await mediator.SendAsync(new AsyncQuery(1));

        result.Should().Be(101);
    }

    [Fact]
    public async Task SendAsync_ShouldInvokeHandler_WhenVoidRequest()
    {
        DoThingHandler.LastTag = null;
        var mediator = BuildMediator();

        await mediator.SendAsync(new DoThing("done"));

        DoThingHandler.LastTag.Should().Be("done");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnUnit_WhenTypedOverloadForVoidRequest()
    {
        var mediator = BuildMediator();

        var unit = await mediator.SendAsync<Unit>(new DoThing("x"));

        unit.Should().Be(Unit.Value);
    }

    [Fact]
    public void IMediator_ShouldResolveAsBothSenderAndPublisher()
    {
        var provider = new ServiceCollection()
            .AddMediator(typeof(MediatorDispatchTests).Assembly)
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        mediator.Should().BeAssignableTo<ISender>();
        mediator.Should().BeAssignableTo<IPublisher>();

        // ISender / IPubliser resolve to the same Mediator type (forwarded registrations).
        provider.GetRequiredService<ISender>().Should().BeOfType<Mediator>();
        provider.GetRequiredService<IPublisher>().Should().BeOfType<Mediator>();
    }

    [Fact]
    public async Task SendAsync_ShouldThrow_WhenNoHandlerRegistered()
    {
        // Mediator registered, but this request type's handler lives in no scanned assembly.
        var sender = new ServiceCollection()
            .AddMediator(typeof(string).Assembly) // System assembly — no handlers
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

        var act = async () => await sender.SendAsync(new SyncQuery(1));

        // The typed dispatch is invoked through reflection (MethodInfo.Invoke), so the
        // "no IRequestHandler registered" InvalidOperationException surfaces wrapped in a
        // TargetInvocationException. (Known rough edge of the reflection-based dispatcher.)
        var ex = await act.Should().ThrowAsync<TargetInvocationException>();
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAsync_ShouldPassCancellationTokenToHandler()
    {
        var mediator = BuildMediator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // AsyncQueryHandler awaits Task.Delay(ct) → observes the cancelled token.
        var act = async () => await mediator.SendAsync(new AsyncQuery(1), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
