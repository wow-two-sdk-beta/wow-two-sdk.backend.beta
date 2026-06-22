using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks;

/// <summary>Provides health-check registration helpers over the built-in <c>IHealthChecksBuilder</c>.</summary>
public static class HealthChecksServiceCollectionExtensions
{
    /// <summary>Adds <c>IHealthChecksBuilder</c> with no checks attached. Add provider checks via the returned builder.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IHealthChecksBuilder AddHealthChecksBuilder(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddHealthChecks();
    }
}
