using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Twilio;

/// <summary>Twilio WhatsApp registration.</summary>
public static class TwilioWhatsAppServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IWhatsAppBroker"/> backed by Twilio's WhatsApp sender under the name <c>twilio</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Account SID, auth token and sender; the host section <c>Comms:WhatsApp:Twilio</c> is applied after it.</param>
    public static IHttpClientBuilder AddTwilioWhatsAppBroker(this IServiceCollection services, Action<TwilioWhatsAppOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            $"{WhatsAppOptions.SectionName}:Twilio",
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.AccountSid), "TwilioWhatsAppOptions.AccountSid must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.AuthToken), "TwilioWhatsAppOptions.AuthToken must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.From), "TwilioWhatsAppOptions.From must not be empty."));
        services.AddWhatsAppBroker<TwilioWhatsAppBroker>(WhatsAppBrokerNameConstants.Twilio);
        return services.AddHttpClient(TwilioWhatsAppBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<TwilioWhatsAppOptions>().BaseAddress);
    }
}
