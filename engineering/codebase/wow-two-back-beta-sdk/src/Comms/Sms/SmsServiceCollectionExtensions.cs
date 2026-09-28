using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms.Eskiz;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms.Twilio;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms.Vonage;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Cross-provider SMS defaults registration.</summary>
public static class SmsServiceCollectionExtensions
{
    /// <summary>Configures the default sender and broker every caller falls back to; pair with a provider registration.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Default sender and broker; the host section <c>Comms:Sms</c> is applied after it.</param>
    public static IServiceCollection AddSmsDefaults(this IServiceCollection services, Action<SmsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddSmsOptions(configure);
    }

    /// <summary>
    /// Registers every SMS broker whose section exists under <c>Comms:Sms</c> (<c>Twilio</c>, <c>Vonage</c>, <c>Eskiz</c>),
    /// so the host configuration alone decides which providers run. Returns the services for chaining.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The host configuration, read now to decide which brokers exist.</param>
    public static IServiceCollection AddSmsBrokers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSmsOptions(null);
        foreach (var provider in configuration.GetSection(SmsOptions.SectionName).GetChildren())
        {
            _ = provider.Key.ToLowerInvariant() switch
            {
                SmsBrokerNameConstants.Twilio => services.AddTwilioSmsBroker(),
                SmsBrokerNameConstants.Vonage => services.AddVonageSmsBroker(),
                SmsBrokerNameConstants.Eskiz => services.AddEskizSmsBroker(),
                _ => null,
            };
        }

        return services;
    }

    internal static IServiceCollection AddSmsOptions(this IServiceCollection services, Action<SmsOptions>? configure)
        => services.AddModuleOptions(
            SmsOptions.SectionName,
            configure,
            builder => builder.Validate(options => options.DefaultFrom is null || !string.IsNullOrWhiteSpace(options.DefaultFrom), "SmsOptions.DefaultFrom must not be blank when supplied."));

    /// <summary>Registers <typeparamref name="TBroker"/> under <paramref name="name"/>; the first broker registered is the default.</summary>
    /// <typeparam name="TBroker">The broker.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="name">The broker name.</param>
    internal static IServiceCollection AddSmsBroker<TBroker>(this IServiceCollection services, string name)
        where TBroker : class, ISmsBroker
    {
        services.TryAddSingleton<TBroker>();
        services.TryAddSingleton<ISmsBroker>(provider => provider.GetRequiredService<TBroker>());
        services.TryAddKeyedSingleton<ISmsBroker>(name, (provider, _) => provider.GetRequiredService<TBroker>());
        services.TryAddSingleton<ISmsBrokerFactory, SmsBrokerFactory>();
        return services;
    }

    /// <summary>The recipient without its leading <c>+</c>, as digit-only providers expect it.</summary>
    /// <param name="to">The E.164 recipient.</param>
    internal static string ToDigits(string to) => to.Trim().TrimStart('+');
}
