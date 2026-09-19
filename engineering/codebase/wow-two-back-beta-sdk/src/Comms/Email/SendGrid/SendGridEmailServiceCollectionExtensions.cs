using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SendGrid;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.SendGrid;

/// <summary>SendGrid sender registration.</summary>
public static class SendGridEmailServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IEmailBroker"/> backed by the SendGrid v3 API; combine with <c>AddEmailDefaults</c> for the default From address.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">API key.</param>
    public static IServiceCollection AddSendGridEmailBroker(
        this IServiceCollection services,
        Action<SendGridEmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddEmailOptions(null);
        services.AddValidatedOptions<SendGridEmailOptions>(
            configure,
            builder => builder.Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "SendGridEmailOptions.ApiKey must not be empty."));
        services.TryAddSingleton<ISendGridClient>(sp =>
        {
            var options = sp.GetRequiredService<SendGridEmailOptions>();
            return new SendGridClient(options.ApiKey);
        });
        services.TryAddSingleton<IEmailBroker, SendGridEmailBroker>();
        return services;
    }
}
