using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

/// <summary>Wires the boot-time interceptor-attachment guard into a service collection.</summary>
internal static class EfInterceptorWiringValidatorServiceCollectionExtensions
{
    /// <summary>Returns the shared <see cref="EfContextWiringRegistry"/>, registering it and the boot guard on first use.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The registry every SDK context registration records itself in.</returns>
    public static EfContextWiringRegistry GetOrAddEfContextWiringRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(EfContextWiringRegistry)
                && descriptor.ImplementationInstance is EfContextWiringRegistry existing)
                return existing;

        var registry = new EfContextWiringRegistry();
        services.AddSingleton(registry);

        // Index 0 so the guard runs before migration/consumer hosted services touch the database.
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, EfInterceptorWiringValidator>());

        return registry;
    }
}
