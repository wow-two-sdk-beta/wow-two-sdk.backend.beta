using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time;

/// <summary>
/// Registration helpers for time abstractions.
/// </summary>
public static class TimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the system-default <see cref="TimeProvider"/> and a NodaTime <see cref="IClock"/>.
    /// </summary>
    public static IServiceCollection AddTimeProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock>(SystemClock.Instance);

        return services;
    }

    /// <summary>
    /// Registers a specific <see cref="TimeProvider"/> instance — useful for tests passing <c>FakeTimeProvider</c>.
    /// </summary>
    public static IServiceCollection AddTimeProviders(this IServiceCollection services, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(timeProvider);

        services.AddSingleton(timeProvider);
        services.TryAddSingleton<IClock>(SystemClock.Instance);

        return services;
    }
}

internal static class ServiceCollectionTryAddExtensions
{
    public static IServiceCollection TryAddSingleton<TService>(this IServiceCollection services, TService instance)
        where TService : class
    {
        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == typeof(TService))
                return services;
        }
        services.AddSingleton(instance);
        return services;
    }
}
