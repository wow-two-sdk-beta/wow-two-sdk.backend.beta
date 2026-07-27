using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;

namespace WoW.Two.Sdk.Backend.Beta.FeatureFlags.Core;

/// <summary>Registers Microsoft.FeatureManagement plus the vendor-neutral <see cref="IFeatureFlags"/> facade.</summary>
public static class FeatureManagementServiceCollectionExtensions
{
    /// <summary>
    /// Adds <c>Microsoft.FeatureManagement</c> (enabling <see cref="IFeatureManager"/>, <c>[FeatureGate]</c>, and
    /// config-driven flags under the <c>FeatureManagement</c> configuration section) and registers the
    /// <see cref="IFeatureFlags"/> facade. Returns the feature-management builder so custom filters can be chained.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The <see cref="IFeatureManagementBuilder"/> for chaining (e.g. <c>.AddFeatureFilter&lt;T&gt;()</c>).</returns>
    public static IFeatureManagementBuilder AddFeatureFlags(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddFeatureManagement();
        services.TryAddScoped<IFeatureFlags, FeatureManagerAdapter>();
        return builder;
    }
}
