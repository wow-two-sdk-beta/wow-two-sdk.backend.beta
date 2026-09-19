using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Verifies startup validation, final topology composition and stable options registration.</summary>
public sealed class OptionsRegistrationTests
{
    [Fact]
    public async Task Invalid_concurrency_fails_when_the_host_starts()
    {
        using var host = Host.CreateDefaultBuilder()
            .UseEnvironment(Environments.Production)
            .ConfigureServices(services => services.AddMessagingConcurrency(options =>
                options.MaxConcurrentMessages = 0))
            .Build();

        var action = () => host.StartAsync();

        await action.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*ConcurrencyOptions.MaxConcurrentMessages*");
    }

    [Fact]
    public void Topology_projects_the_final_provider_composition()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddRabbitMqEventBus(options => options.Queue = "orders", typeof(string).Assembly)
            .BuildServiceProvider();

        var topology = provider.GetRequiredService<TopologyOptions>();

        topology.SharedEndpointName.Should().Be("orders");
        provider.GetRequiredService<IOptions<TopologyOptions>>().Value.Should().BeSameAs(topology);
    }

    [Fact]
    public void Delayed_retry_resolution_does_not_mutate_the_service_collection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDelayedEventRetry();
        var count = services.Count;

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<DelayedRetryOptions>().Enabled.Should().BeTrue();
        services.Should().HaveCount(count);
    }
}
