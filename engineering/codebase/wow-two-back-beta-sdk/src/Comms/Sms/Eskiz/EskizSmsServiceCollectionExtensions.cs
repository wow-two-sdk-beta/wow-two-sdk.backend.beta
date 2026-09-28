using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Eskiz;

/// <summary>Eskiz (Uzbekistan) SMS registration.</summary>
public static class EskizSmsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISmsBroker"/> backed by the Eskiz gateway.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Account email, password and sender nickname; the host section <c>Comms:Sms:Eskiz</c> is applied after it.</param>
    public static IHttpClientBuilder AddEskizSmsBroker(this IServiceCollection services, Action<EskizSmsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSmsOptions(null);
        services.AddModuleOptions(
            $"{SmsOptions.SectionName}:Eskiz",
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.Email), "EskizSmsOptions.Email must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "EskizSmsOptions.Password must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.From), "EskizSmsOptions.From must not be empty."));
        services.AddSmsBroker<EskizSmsBroker>(SmsBrokerNameConstants.Eskiz);
        return services.AddHttpClient(EskizSmsBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<EskizSmsOptions>().BaseAddress);
    }
}
