using Amazon;
using Amazon.SimpleEmailV2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;

namespace WoW.Two.Sdk.Backend.Beta.Comms.EmailSes;

/// <summary>Amazon SES sender registration.</summary>
public static class SesEmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEmailSender"/> backed by Amazon SES v2. Credentials resolve through the
    /// standard AWS chain (env vars, profile, IMDS). Combine with <c>AddEmailDefaults</c> for the
    /// default From address.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional region override.</param>
    public static IServiceCollection AddSesEmailSender(
        this IServiceCollection services,
        Action<SesEmailOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<SesEmailOptions>();
        }

        services.TryAddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SesEmailOptions>>().Value;
            return options.Region is null
                ? new AmazonSimpleEmailServiceV2Client()
                : new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(options.Region));
        });
        services.TryAddSingleton<IEmailSender, SesEmailSender>();
        return services;
    }
}
