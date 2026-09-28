using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Sms;

/// <summary>SMS OTP delivery registration.</summary>
public static class SmsOtpServiceCollectionExtensions
{
    /// <summary>Registers <see cref="SmsOtpDeliveryHandler"/> as an additive <see cref="IOtpDeliveryHandler"/>; requires a registered <c>ISmsBroker</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional message template / scope display-name overrides.</param>
    public static IServiceCollection AddSmsOtpDelivery(this IServiceCollection services, Action<SmsOtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddValidatedOptions(
            configure,
            builder => builder.Validate(options => options.MessageTemplate.Contains("{1}", StringComparison.Ordinal), "SmsOtpOptions.MessageTemplate must contain the {1} code placeholder."));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOtpDeliveryHandler, SmsOtpDeliveryHandler>());
        return services;
    }
}
