using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenFeature;

namespace WoW.Two.Sdk.Backend.Beta.FeatureFlags.OpenFeature;

/// <summary>
/// Registers an OpenFeature <see cref="IFeatureClient"/> — the vendor-neutral seam that vendor SDKs
/// (LaunchDarkly, ConfigCat, GrowthBook, Unleash, …) implement as providers. Set the provider once at
/// startup with <c>await Api.Instance.SetProviderAsync(provider)</c>.
/// </summary>
public static class OpenFeatureServiceCollectionExtensions
{
    /// <summary>Registers an OpenFeature <see cref="IFeatureClient"/> resolved from the global API instance.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="clientName">Optional named client (for scoping to a provider domain); default the unnamed client.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOpenFeatureClient(this IServiceCollection services, string? clientName = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFeatureClient>(_ =>
            clientName is null ? Api.Instance.GetClient() : Api.Instance.GetClient(clientName));

        return services;
    }
}
