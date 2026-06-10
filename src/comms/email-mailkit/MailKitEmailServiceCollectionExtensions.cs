using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.MailKit;

/// <summary>MailKit SMTP sender registration.</summary>
public static class MailKitEmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEmailSender"/> backed by SMTP via MailKit.
    /// Combine with <c>AddEmailDefaults</c> for the default From address.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">SMTP host / port / credentials.</param>
    public static IServiceCollection AddMailKitEmailSender(
        this IServiceCollection services,
        Action<MailKitEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton<IEmailSender, MailKitEmailSender>();
        return services;
    }
}
