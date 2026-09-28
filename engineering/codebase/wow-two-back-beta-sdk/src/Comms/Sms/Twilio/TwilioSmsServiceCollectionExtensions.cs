using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Twilio;

/// <summary>Twilio SMS registration.</summary>
public static class TwilioSmsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISmsBroker"/> backed by the Twilio Messages API; combine with <c>AddSmsDefaults</c> for the sender.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Account SID, auth token and optional messaging service; the host section <c>Comms:Sms:Twilio</c> is applied after it.</param>
    public static IHttpClientBuilder AddTwilioSmsBroker(this IServiceCollection services, Action<TwilioSmsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSmsOptions(null);
        services.AddModuleOptions(
            $"{SmsOptions.SectionName}:Twilio",
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.AccountSid), "TwilioSmsOptions.AccountSid must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.AuthToken), "TwilioSmsOptions.AuthToken must not be empty."));
        services.AddSmsBroker<TwilioSmsBroker>(SmsBrokerNameConstants.Twilio);
        return services.AddHttpClient(TwilioSmsBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<TwilioSmsOptions>().BaseAddress);
    }
}
