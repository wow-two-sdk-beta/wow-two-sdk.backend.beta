using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Vonage;

/// <summary>Vonage SMS registration.</summary>
public static class VonageSmsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISmsBroker"/> backed by the Vonage SMS API; combine with <c>AddSmsDefaults</c> for the sender.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">API key and secret; the host section <c>Comms:Sms:Vonage</c> is applied after it.</param>
    public static IHttpClientBuilder AddVonageSmsBroker(this IServiceCollection services, Action<VonageSmsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSmsOptions(null);
        services.AddModuleOptions(
            $"{SmsOptions.SectionName}:Vonage",
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "VonageSmsOptions.ApiKey must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiSecret), "VonageSmsOptions.ApiSecret must not be empty."));
        services.AddSmsBroker<VonageSmsBroker>(SmsBrokerNameConstants.Vonage);
        return services.AddHttpClient(VonageSmsBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<VonageSmsOptions>().BaseAddress);
    }
}
