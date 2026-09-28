using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Vonage;

/// <summary>Vonage SMS registration.</summary>
public static class VonageSmsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISmsBroker"/> backed by the Vonage SMS API; combine with <c>AddSmsDefaults</c> for the sender.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">API key and secret.</param>
    public static IHttpClientBuilder AddVonageSmsBroker(this IServiceCollection services, Action<VonageSmsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSmsOptions(null);
        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "VonageSmsOptions.ApiKey must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiSecret), "VonageSmsOptions.ApiSecret must not be empty."));
        services.TryAddSingleton<ISmsBroker, VonageSmsBroker>();
        return services.AddHttpClient(VonageSmsBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<VonageSmsOptions>().BaseAddress);
    }
}
