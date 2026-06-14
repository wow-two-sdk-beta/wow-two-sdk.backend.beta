using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>Cross-provider email defaults registration.</summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Sets the default From / Reply-To applied by every <see cref="IEmailSender"/> when a message
    /// doesn't carry its own. Pair with a provider registration
    /// (<c>AddMailKitEmailSender</c> / <c>AddSendGridEmailSender</c> / <c>AddSesEmailSender</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
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
