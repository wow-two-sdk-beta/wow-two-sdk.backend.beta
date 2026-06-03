using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks;

/// <summary>
/// Health-check registration helpers. Wraps the built-in `IHealthChecksBuilder` so consumers don't need to discover the right extension method package.
/// </summary>
public static class HealthChecksServiceCollectionExtensions
{
    /// <summary>
    /// Registers `IHealthChecksBuilder` with no checks attached. Add provider checks via the returned builder.
    /// </summary>
    public static IHealthChecksBuilder AddHealthChecksBuilder(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddHealthChecks();
    }
}
