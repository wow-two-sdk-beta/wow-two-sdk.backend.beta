using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using NodaTime;
using WoW.Two.Sdk.Backend.Beta.Foundation.Time;
using WoW.Two.Sdk.Backend.Beta.Testing;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Testing;

/// <summary>Exercises test-host configuration and clock isolation without process-global state.</summary>
public sealed class WebApiTestHostIsolationTests
{
    [Fact]
    public void TwoHosts_ShouldKeepConfigurationAndBothClockSurfacesIndependent()
    {
        var key = $"WOW_TWO_TEST_HOST_ISOLATION_{Guid.NewGuid():N}";
        var prior = Environment.GetEnvironmentVariable(key);
        using var firstFactory = CreateFactory("first", new DateTimeOffset(2031, 1, 2, 3, 4, 5, TimeSpan.Zero));
        using var secondFactory = CreateFactory("second", new DateTimeOffset(2041, 2, 3, 4, 5, 6, TimeSpan.Zero));
        using var firstHost = BuildHost(firstFactory);
        using var secondHost = BuildHost(secondFactory);

        AssertHost(firstHost.Services, firstFactory.Clock, "first");
        AssertHost(secondHost.Services, secondFactory.Clock, "second");

        firstFactory.Clock.Advance(TimeSpan.FromDays(1));

        firstHost.Services.GetRequiredService<IClock>().GetCurrentInstant()
            .Should().Be(Instant.FromDateTimeOffset(firstFactory.Clock.GetUtcNow()));
        secondHost.Services.GetRequiredService<IClock>().GetCurrentInstant()
            .Should().Be(Instant.FromDateTimeOffset(secondFactory.Clock.GetUtcNow()));
        Environment.GetEnvironmentVariable(key).Should().Be(prior);
    }

    [Fact]
    public void Host_ShouldRejectCaptiveScopedDependencies_WhenUsingDefaultEnvironment()
    {
        using var factory = CreateFactory("scope-validation", DateTimeOffset.UtcNow);

        var failure = Assert.Throws<AggregateException>(() =>
        {
            using var host = BuildHost(factory, services =>
            {
                services.AddScoped<ScopedDependency>();
                services.AddSingleton<CaptiveDependency>();
            });
        });

        failure.ToString().Should().Contain("Cannot consume scoped service");
    }

    private static ExposedWebApiTestHost CreateFactory(string value, DateTimeOffset utcNow)
    {
        var factory = new ExposedWebApiTestHost
        {
            ConfigureConfigurationHook = configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HostIsolation:Value"] = value
                })
        };
        factory.Clock.SetUtcNow(utcNow);
        return factory;
    }

    private static WebApplication BuildHost(ExposedWebApiTestHost factory, Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddEnvironmentVariables();
        builder.Services.AddTimeProviders();
        configure?.Invoke(builder.Services);

        factory.Apply(builder.WebHost);
        return builder.Build();
    }

    private static void AssertHost(IServiceProvider services, FakeTimeProvider expectedClock, string expectedValue)
    {
        services.GetRequiredService<IConfiguration>()["HostIsolation:Value"].Should().Be(expectedValue);
        services.GetRequiredService<IHostEnvironment>().EnvironmentName.Should().Be(Environments.Development);
        services.GetRequiredService<TimeProvider>().Should().BeSameAs(expectedClock);
        services.GetRequiredService<IClock>().GetCurrentInstant()
            .Should().Be(Instant.FromDateTimeOffset(expectedClock.GetUtcNow()));
    }

    private sealed class EntryPoint;

    private sealed class ScopedDependency;

    private sealed class CaptiveDependency(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class ExposedWebApiTestHost : WebApiTestHost<EntryPoint>
    {
        public void Apply(IWebHostBuilder builder) => ConfigureWebHost(builder);
    }
}
