using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.FeatureGates;

/// <summary>Registers the mediator feature gate.</summary>
public static class FeatureGatingServiceCollectionExtensions
{
    /// <summary>Adds <see cref="FeatureGatingInterceptor{TRequest,TResponse}"/>; needs <c>AddFeatureFlags()</c> for <c>IFeatureFlags</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorFeatureGatingInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddMediatorInterceptor(typeof(FeatureGatingInterceptor<,>));
    }
}
