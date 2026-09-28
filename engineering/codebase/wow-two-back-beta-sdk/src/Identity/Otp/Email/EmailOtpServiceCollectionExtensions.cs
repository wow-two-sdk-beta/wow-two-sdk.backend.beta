using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Email;

/// <summary>Email OTP delivery registration.</summary>
public static class EmailOtpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EmailOtpDeliveryHandler"/> for the <c>email</c> channel; requires a registered email broker.
    /// Options come from <paramref name="configure"/>, then the host section <c>Identity:Otp:Email</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The sender.</param>
    public static IServiceCollection AddEmailOtpDelivery(this IServiceCollection services, Action<EmailOtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(EmailOtpOptions.SectionName, configure);
        services.AddOtpDeliveryHandler<EmailOtpDeliveryHandler>(OtpChannelNameConstants.Email);
        return services;
    }
}
