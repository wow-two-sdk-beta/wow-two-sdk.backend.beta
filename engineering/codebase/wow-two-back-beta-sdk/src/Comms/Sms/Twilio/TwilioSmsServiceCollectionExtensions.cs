using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Twilio;

/// <summary>Twilio SMS registration.</summary>
public static class TwilioSmsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISmsBroker"/> backed by the Twilio Messages API; combine with <c>AddSmsDefaults</c> for the sender.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Account SID, auth token and optional messaging service.</param>
    public static IHttpClientBuilder AddTwilioSmsBroker(this IServiceCollection services, Action<TwilioSmsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSmsOptions(null);
        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.AccountSid), "TwilioSmsOptions.AccountSid must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.AuthToken), "TwilioSmsOptions.AuthToken must not be empty."));
        services.TryAddSingleton<ISmsBroker, TwilioSmsBroker>();
        return services.AddHttpClient(TwilioSmsBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<TwilioSmsOptions>().BaseAddress);
    }
}
