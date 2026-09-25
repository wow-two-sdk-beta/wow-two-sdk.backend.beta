using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.DataUnits;

/// <summary>Registers optional transactional request execution.</summary>
public static class DataUnitServiceCollectionExtensions
{
    /// <summary>Registers data units before deduplication; requires a separately registered data session.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorDataUnitInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Transient(
            typeof(IRequestInterceptor<,>), typeof(DataUnitInterceptor<,>)));
        return services;
    }
}
