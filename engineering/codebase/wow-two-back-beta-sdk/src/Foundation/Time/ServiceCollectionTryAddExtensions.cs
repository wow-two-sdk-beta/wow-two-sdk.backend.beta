using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time;

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
