using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

/// <summary>
/// <c>AddMediator</c> registration contract — assembly scan picks up request / notification / CQRS handlers,
/// registers them transient, and is idempotent (<c>TryAdd</c>) across repeated calls.
/// </summary>
public sealed class AddMediatorTests
{
    private sealed record ScanQuery(int N) : IQuery<int>;

    private sealed class ScanQueryHandler : IQueryHandler<ScanQuery, int>
    {
        public ValueTask<int> HandleAsync(ScanQuery request, CancellationToken cancellationToken)
            => ValueTask.FromResult(request.N);
    }

    private sealed record ScanNote : INotification;

    private sealed class ScanNoteHandlerA : INotificationHandler<ScanNote>
    {
        public ValueTask HandleAsync(ScanNote notification, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class ScanNoteHandlerB : INotificationHandler<ScanNote>
    {
        public ValueTask HandleAsync(ScanNote notification, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    [Fact]
    public void AddMediator_ShouldRegisterTheMediatorTrio()
    {
        var services = new ServiceCollection().AddMediator(typeof(AddMediatorTests).Assembly);

        services.Should().Contain(d => d.ServiceType == typeof(IMediator));
        services.Should().Contain(d => d.ServiceType == typeof(ISender));
        services.Should().Contain(d => d.ServiceType == typeof(IPublisher));
    }

    [Fact]
    public void AddMediator_ShouldScanAndBindRequestHandlers()
    {
        var provider = new ServiceCollection()
            .AddMediator(typeof(AddMediatorTests).Assembly)
            .BuildServiceProvider();

        provider.GetService<IRequestHandler<ScanQuery, int>>().Should().BeOfType<ScanQueryHandler>();
    }

    [Fact]
    public void AddMediator_ShouldScanAndBindAllNotificationHandlers()
    {
        var provider = new ServiceCollection()
            .AddMediator(typeof(AddMediatorTests).Assembly)
            .BuildServiceProvider();

        var handlers = provider.GetServices<INotificationHandler<ScanNote>>().ToList();

        handlers.Should().HaveCountGreaterThanOrEqualTo(2);
        handlers.Should().Contain(h => h is ScanNoteHandlerA);
        handlers.Should().Contain(h => h is ScanNoteHandlerB);
    }

    [Fact]
    public void AddMediator_ShouldRegisterHandlersAsTransient()
    {
        var services = new ServiceCollection().AddMediator(typeof(AddMediatorTests).Assembly);

        var handlerDescriptor = services.Single(d =>
            d.ServiceType == typeof(IRequestHandler<ScanQuery, int>));

        handlerDescriptor.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddMediator_ShouldRegisterMediatorAsTransient()
    {
        var services = new ServiceCollection().AddMediator(typeof(AddMediatorTests).Assembly);

        services.Single(d => d.ServiceType == typeof(IMediator))
            .Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddMediator_ShouldResolveFreshHandlerEachTime()
    {
        var provider = new ServiceCollection()
            .AddMediator(typeof(AddMediatorTests).Assembly)
            .BuildServiceProvider();

        var first = provider.GetRequiredService<IRequestHandler<ScanQuery, int>>();
        var second = provider.GetRequiredService<IRequestHandler<ScanQuery, int>>();

        first.Should().NotBeSameAs(second); // transient → new instance per resolve
    }

    [Fact]
    public void AddMediator_ShouldBeIdempotentForTheMediatorRegistration()
    {
        // TryAdd guards the IMediator/ISender/IPublisher registrations — calling twice must not duplicate them.
        var services = new ServiceCollection()
            .AddMediator(typeof(AddMediatorTests).Assembly)
            .AddMediator(typeof(AddMediatorTests).Assembly);

        services.Count(d => d.ServiceType == typeof(IMediator)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(ISender)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IPublisher)).Should().Be(1);
    }

    [Fact]
    public void AddMediator_ShouldScanCallingAssembly_WhenDefaultOverload()
    {
        // No assembly argument → scans the assembly that called AddMediator (this test assembly).
        var provider = new ServiceCollection()
            .AddMediator()
            .BuildServiceProvider();

        provider.GetService<IRequestHandler<ScanQuery, int>>().Should().BeOfType<ScanQueryHandler>();
    }

    [Fact]
    public void AddMediator_ShouldThrow_WhenServicesNull()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMediator(typeof(AddMediatorTests).Assembly);

        act.Should().Throw<ArgumentNullException>();
    }
}
