using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SendGrid;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.SendGrid;

/// <summary>SendGrid sender registration.</summary>
public static class SendGridEmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEmailSender"/> backed by the SendGrid v3 API.
    /// Combine with <c>AddEmailDefaults</c> for the default From address.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">API key.</param>
    public static IServiceCollection AddSendGridEmailSender(
        this IServiceCollection services,
        Action<SendGridEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton<ISendGridClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SendGridEmailOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new InvalidOperationException("SendGridEmailOptions.ApiKey must be configured.");
            }

            return new SendGridClient(options.ApiKey);
        });
        services.TryAddSingleton<IEmailSender, SendGridEmailSender>();
        return services;
    }
}
