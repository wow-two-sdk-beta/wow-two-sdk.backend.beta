using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.Apns;

/// <summary>APNs push registration.</summary>
public static class ApnsPushServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IPushBroker"/> backed by APNs with token-based (<c>.p8</c>) authentication.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Team id, key id, PEM key and bundle id.</param>
    public static IHttpClientBuilder AddApnsPushBroker(this IServiceCollection services, Action<ApnsPushOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.TeamId), "ApnsPushOptions.TeamId must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.KeyId), "ApnsPushOptions.KeyId must not be empty.")
                .Validate(o => o.PrivateKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal), "ApnsPushOptions.PrivateKeyPem must hold the .p8 PEM.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.BundleId), "ApnsPushOptions.BundleId must not be empty."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPushBroker, ApnsPushBroker>();
        return services
            .AddHttpClient(ApnsPushBroker.HttpClientName, (serviceProvider, http) =>
            {
                var options = serviceProvider.GetRequiredService<ApnsPushOptions>();
                http.BaseAddress = options.BaseAddress
                    ?? new Uri(options.UseSandbox ? "https://api.sandbox.push.apple.com/" : "https://api.push.apple.com/");
                http.DefaultRequestVersion = System.Net.HttpVersion.Version20;
            });
    }
}
