using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.Fcm;

/// <summary>Firebase Cloud Messaging registration.</summary>
public static class FcmPushServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IPushBroker"/> backed by FCM HTTP v1 with a service-account key.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The service-account JSON.</param>
    public static IHttpClientBuilder AddFcmPushBroker(this IServiceCollection services, Action<FcmPushOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            builder => builder.Validate(o => o.ServiceAccountJson.Contains("\"private_key\"", StringComparison.Ordinal), "FcmPushOptions.ServiceAccountJson must hold a service-account key."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPushBroker, FcmPushBroker>();
        return services.AddHttpClient(FcmPushBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<FcmPushOptions>().BaseAddress);
    }
}
