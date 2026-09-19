using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.MailKit;

/// <summary>MailKit SMTP sender registration.</summary>
public static class MailKitEmailServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IEmailBroker"/> backed by SMTP via MailKit; combine with <c>AddEmailDefaults</c> for the default From address.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">SMTP host / port / credentials.</param>
    public static IServiceCollection AddMailKitEmailBroker(
        this IServiceCollection services,
        Action<MailKitEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddEmailOptions(null);
        services.AddValidatedOptions<MailKitEmailOptions>(
            configure,
            builder => builder
                .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "MailKitEmailOptions.Host must not be empty.")
                .Validate(options => options.Port is > 0 and <= 65_535, "MailKitEmailOptions.Port must be between 1 and 65535.")
                .Validate(options => Enum.IsDefined(options.SecureSocket), "MailKitEmailOptions.SecureSocket must be a defined mode."));
        services.TryAddSingleton<IEmailBroker, MailKitEmailBroker>();
        return services;
    }
}
