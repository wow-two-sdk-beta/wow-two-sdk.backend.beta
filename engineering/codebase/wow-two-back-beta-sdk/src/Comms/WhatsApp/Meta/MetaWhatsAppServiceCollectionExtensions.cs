using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Meta;

/// <summary>WhatsApp Cloud API registration.</summary>
public static class MetaWhatsAppServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IWhatsAppBroker"/> backed by Meta's WhatsApp Cloud API under the name <c>meta</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Access token and phone-number id; the host section <c>Comms:WhatsApp:Meta</c> is applied after it.</param>
    public static IHttpClientBuilder AddMetaWhatsAppBroker(this IServiceCollection services, Action<MetaWhatsAppOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            $"{WhatsAppOptions.SectionName}:Meta",
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.AccessToken), "MetaWhatsAppOptions.AccessToken must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.PhoneNumberId), "MetaWhatsAppOptions.PhoneNumberId must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ApiVersion), "MetaWhatsAppOptions.ApiVersion must not be empty."));
        services.AddWhatsAppBroker<MetaWhatsAppBroker>(WhatsAppBrokerNameConstants.Meta);
        return services.AddHttpClient(MetaWhatsAppBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<MetaWhatsAppOptions>().BaseAddress);
    }
}
