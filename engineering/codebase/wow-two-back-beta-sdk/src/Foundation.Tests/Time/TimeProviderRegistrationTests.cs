using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using WoW.Two.Sdk.Backend.Beta.Foundation.Time;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Time;

/// <summary>Verifies that both supported clock abstractions observe one registered source.</summary>
public sealed class TimeProviderRegistrationTests
{
    [Fact]
    public void AddTimeProviders_WithInstance_ShouldAdaptTheSameInstantToNodaTime()
    {
        var expected = new DateTimeOffset(2034, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var services = new ServiceCollection();
        services.AddTimeProviders(new FixedTimeProvider(expected));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().GetUtcNow().Should().Be(expected);
        provider.GetRequiredService<IClock>().GetCurrentInstant()
            .Should().Be(Instant.FromDateTimeOffset(expected));
    }

    [Fact]
    public void AddTimeProviders_ShouldResolveIClockFromTheFinalTimeProviderRegistration()
    {
        var expected = new DateTimeOffset(2035, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var services = new ServiceCollection();
        services.AddTimeProviders();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(expected));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IClock>().GetCurrentInstant()
            .Should().Be(Instant.FromDateTimeOffset(expected));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
