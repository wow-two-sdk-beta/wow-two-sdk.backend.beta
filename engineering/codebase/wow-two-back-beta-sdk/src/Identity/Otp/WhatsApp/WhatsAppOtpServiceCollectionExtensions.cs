using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.WhatsApp;

/// <summary>WhatsApp OTP delivery registration.</summary>
public static class WhatsAppOtpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WhatsAppOtpDeliveryHandler"/> for the <c>whatsapp</c> channel; requires a registered WhatsApp
    /// broker. Options come from <paramref name="configure"/>, then the host section <c>Identity:Otp:WhatsApp</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Broker choice and authentication template.</param>
    public static IServiceCollection AddWhatsAppOtpDelivery(this IServiceCollection services, Action<WhatsAppOtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            WhatsAppOtpOptions.SectionName,
            configure,
            builder => builder.Validate(o => !string.IsNullOrWhiteSpace(o.TemplateLanguage), "WhatsAppOtpOptions.TemplateLanguage must not be empty."));
        services.AddOtpDeliveryHandler<WhatsAppOtpDeliveryHandler>(OtpChannelNameConstants.WhatsApp);
        return services;
    }
}
