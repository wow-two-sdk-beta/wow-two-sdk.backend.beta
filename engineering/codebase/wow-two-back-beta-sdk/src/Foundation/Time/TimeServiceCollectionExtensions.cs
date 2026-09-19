using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time;

/// <summary>Provides registration helpers for time abstractions.</summary>
public static class TimeServiceCollectionExtensions
{
    /// <summary>Registers the system-default <see cref="TimeProvider"/> and adapts it to NodaTime <see cref="IClock"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddTimeProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock>(static provider =>
            new TimeProviderClock(provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    /// <summary>Registers a specific <see cref="TimeProvider"/> and adapts it to NodaTime <see cref="IClock"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="timeProvider">The time provider instance to register.</param>
    public static IServiceCollection AddTimeProviders(this IServiceCollection services, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(timeProvider);

        services.AddSingleton(timeProvider);
        services.TryAddSingleton<IClock>(static provider =>
            new TimeProviderClock(provider.GetRequiredService<TimeProvider>()));

        return services;
    }
}

internal sealed class TimeProviderClock(TimeProvider timeProvider) : IClock
{
    public Instant GetCurrentInstant() => Instant.FromDateTimeOffset(timeProvider.GetUtcNow());
}
