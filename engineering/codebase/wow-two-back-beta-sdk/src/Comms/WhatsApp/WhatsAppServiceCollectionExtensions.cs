using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Meta;
using WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Twilio;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Cross-provider WhatsApp registration.</summary>
public static class WhatsAppServiceCollectionExtensions
{
    /// <summary>
    /// Registers every WhatsApp broker whose section exists under <c>Comms:WhatsApp</c> (<c>Meta</c>, <c>Twilio</c>), so
    /// the host configuration alone decides which providers run.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The host configuration, read now to decide which brokers exist.</param>
    public static IServiceCollection AddWhatsAppBrokers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddWhatsAppOptions();
        foreach (var provider in configuration.GetSection(WhatsAppOptions.SectionName).GetChildren())
        {
            _ = provider.Key.ToLowerInvariant() switch
            {
                WhatsAppBrokerNameConstants.Meta => services.AddMetaWhatsAppBroker(),
                WhatsAppBrokerNameConstants.Twilio => services.AddTwilioWhatsAppBroker(),
                _ => null,
            };
        }

        return services;
    }

    internal static IServiceCollection AddWhatsAppOptions(this IServiceCollection services)
        => services.AddModuleOptions<WhatsAppOptions>(WhatsAppOptions.SectionName, configure: null);

    /// <summary>Registers <typeparamref name="TBroker"/> under <paramref name="name"/>; the first broker registered is the default.</summary>
    /// <typeparam name="TBroker">The broker.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="name">The broker name.</param>
    internal static IServiceCollection AddWhatsAppBroker<TBroker>(this IServiceCollection services, string name)
        where TBroker : class, IWhatsAppBroker
    {
        services.AddWhatsAppOptions();
        services.TryAddSingleton<TBroker>();
        services.TryAddSingleton<IWhatsAppBroker>(provider => provider.GetRequiredService<TBroker>());
        services.TryAddKeyedSingleton<IWhatsAppBroker>(name, (provider, _) => provider.GetRequiredService<TBroker>());
        services.TryAddSingleton<IWhatsAppBrokerFactory, WhatsAppBrokerFactory>();
        return services;
    }

    /// <summary>The recipient without its leading <c>+</c>, as the Cloud API expects it.</summary>
    /// <param name="to">The E.164 recipient.</param>
    internal static string ToDigits(string to) => to.Trim().TrimStart('+');
}
