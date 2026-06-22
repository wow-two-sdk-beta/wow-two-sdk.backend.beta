using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>Cross-provider email defaults registration.</summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>Configures the default From / Reply-To every <see cref="IEmailSender"/> falls back to; pair with a provider registration.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Default sender / reply-to.</param>
    public static IServiceCollection AddEmailDefaults(
        this IServiceCollection services,
        Action<EmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        return services;
    }
}
