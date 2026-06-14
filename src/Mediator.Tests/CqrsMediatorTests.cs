using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

public sealed class CqrsMediatorTests
{
    // --- CQRS requests + handlers (markers layered over IRequest / IRequestHandler) ---

    private sealed record Ping(string Text) : IQuery<string>;

    private sealed class PingHandler : IQueryHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken)
            => Task.FromResult($"pong:{request.Text}");
    }

    private sealed record Add(int A, int B) : ICommand<int>;

    private sealed class AddHandler : ICommandHandler<Add, int>
    {
        public Task<int> Handle(Add request, CancellationToken cancellationToken)
            => Task.FromResult(request.A + request.B);
    }

    private sealed record Touch : ICommand;

    private sealed class TouchHandler : ICommandHandler<Touch>
    {
        public static int Count;

        public Task<Unit> Handle(Touch request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Count);
            return Unit.Task;
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

    // --- SendAsync facade forwards to ISender.Send for each overload ---

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
    public async Task SendAsync_query_equals_Send()
    {
        var sender = BuildSender();

        var viaFacade = await sender.SendAsync(new Ping("x"));
        var viaSend = await sender.Send(new Ping("x"));

        viaFacade.Should().Be(viaSend);
    }

    [Fact]
    public async Task SendAsync_throws_on_null_sender()
    {
        ISender sender = null!;
        var act = async () => await sender.SendAsync(new Ping("x"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
