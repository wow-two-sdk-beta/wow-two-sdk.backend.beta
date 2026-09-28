using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Sms;

/// <summary>SMS OTP delivery registration.</summary>
public static class SmsOtpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SmsOtpDeliveryHandler"/> for the <c>sms</c> channel (keyed, and additive for callers that
    /// enumerate handlers); requires a registered SMS broker. Options come from <paramref name="configure"/>, then the
    /// host section <c>Identity:Otp:Sms</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Broker choice, fallback template and scope display names.</param>
    public static IServiceCollection AddSmsOtpDelivery(this IServiceCollection services, Action<SmsOtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            SmsOtpOptions.SectionName,
            configure,
            builder => builder.Validate(options => options.MessageTemplate.Contains("{1}", StringComparison.Ordinal), "SmsOtpOptions.MessageTemplate must contain the {1} code placeholder."));
        services.AddOtpDeliveryHandler<SmsOtpDeliveryHandler>(OtpChannelNameConstants.Sms);
        return services;
    }
}
