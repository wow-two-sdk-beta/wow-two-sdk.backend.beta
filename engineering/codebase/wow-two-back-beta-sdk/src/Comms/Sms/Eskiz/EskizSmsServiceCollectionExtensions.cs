using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Eskiz;

/// <summary>Eskiz (Uzbekistan) SMS registration.</summary>
public static class EskizSmsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISmsBroker"/> backed by the Eskiz gateway.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Account email, password and sender nickname.</param>
    public static IHttpClientBuilder AddEskizSmsBroker(this IServiceCollection services, Action<EskizSmsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSmsOptions(null);
        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => !string.IsNullOrWhiteSpace(o.Email), "EskizSmsOptions.Email must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "EskizSmsOptions.Password must not be empty.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.From), "EskizSmsOptions.From must not be empty."));
        services.TryAddSingleton<ISmsBroker, EskizSmsBroker>();
        return services.AddHttpClient(EskizSmsBroker.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<EskizSmsOptions>().BaseAddress);
    }
}
