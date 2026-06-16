using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

public sealed class CqrsMediatorTests
{
    // --- CQRS requests + handlers (markers layered over IRequest / IRequestHandler) ---

    private sealed record Ping(string Text) : IQuery<string>;

    private sealed class PingHandler : IQueryHandler<Ping, string>
    {
        public ValueTask<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => ValueTask.FromResult($"pong:{request.Text}");
    }

    private sealed record Add(int A, int B) : ICommand<int>;

    private sealed class AddHandler : ICommandHandler<Add, int>
    {
        public ValueTask<int> HandleAsync(Add request, CancellationToken cancellationToken)
            => ValueTask.FromResult(request.A + request.B);
    }

    private sealed record Touch : ICommand;

    private sealed class TouchHandler : ICommandHandler<Touch>
    {
        public static int Count;

        public ValueTask<Unit> HandleAsync(Touch request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Count);
            return Unit.ValueTask;
        }
    }

    private static ISender BuildSender()
        => new ServiceCollection()
            .AddMediator(typeof(CqrsMediatorTests).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

    // --- the existing DI scan binds CQRS handlers as the primitive IRequestHandler<,> the dispatcher
    //     looks up — no scanner change needed. (The scan whitelists IRequestHandler<,> / INotificationHandler<>;
    //     a refinement like IQueryHandler<,> is reachable via its base, which is the type dispatch resolves.) ---

    [Fact]
    public void AddMediator_binds_cqrs_handlers_via_their_base_request_handler()
    {
        var provider = new ServiceCollection()
            .AddMediator(typeof(CqrsMediatorTests).Assembly)
            .BuildServiceProvider();

        // IQueryHandler<Ping,string> : IRequestHandler<Ping,string> — dispatch looks up the base.
        provider.GetService<IRequestHandler<Ping, string>>().Should().BeOfType<PingHandler>();
        provider.GetService<IRequestHandler<Add, int>>().Should().BeOfType<AddHandler>();
        provider.GetService<IRequestHandler<Touch, Unit>>().Should().BeOfType<TouchHandler>();
    }

    // --- SendAsync (native on ISender) dispatches each CQRS shape; a query IS an IRequest<T> ---

    [Fact]
    public async Task SendAsync_dispatches_a_query()
    {
        var sender = BuildSender();

        var result = await sender.SendAsync(new Ping("hi"));

        result.Should().Be("pong:hi");
    }

    [Fact]
    public async Task SendAsync_dispatches_a_command_with_result()
    {
        var sender = BuildSender();

        var result = await sender.SendAsync(new Add(2, 3));

        result.Should().Be(5);
    }

    [Fact]
    public async Task SendAsync_dispatches_a_void_command()
    {
        TouchHandler.Count = 0;
        var sender = BuildSender();

        await sender.SendAsync(new Touch());

        TouchHandler.Count.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_void_command_returns_unit()
    {
        var sender = BuildSender();

        var unit = await sender.SendAsync((IRequest)new Touch());

        unit.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task SendAsync_throws_on_null_request()
    {
        var sender = BuildSender();
        var act = async () => await sender.SendAsync<string>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
