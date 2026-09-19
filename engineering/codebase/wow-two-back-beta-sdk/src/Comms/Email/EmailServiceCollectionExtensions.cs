using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>Cross-provider email defaults registration.</summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>Configures the default From / Reply-To every <see cref="IEmailBroker"/> falls back to; pair with a provider registration.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Default sender / reply-to.</param>
    public static IServiceCollection AddEmailDefaults(
        this IServiceCollection services,
        Action<EmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddEmailOptions(configure);
    }

    internal static IServiceCollection AddEmailOptions(
        this IServiceCollection services,
        Action<EmailOptions>? configure) =>
        services.AddValidatedOptions<EmailOptions>(
            configure,
            builder => builder
                .Validate(options => options.DefaultFrom is null || !string.IsNullOrWhiteSpace(options.DefaultFrom.Address), "EmailOptions.DefaultFrom.Address must not be empty when supplied.")
                .Validate(options => options.DefaultReplyTo is null || !string.IsNullOrWhiteSpace(options.DefaultReplyTo.Address), "EmailOptions.DefaultReplyTo.Address must not be empty when supplied."));
}
