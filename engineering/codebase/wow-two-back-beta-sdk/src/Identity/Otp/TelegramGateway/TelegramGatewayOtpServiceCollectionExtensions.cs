using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.TelegramGateway;

/// <summary>Telegram Gateway OTP delivery registration.</summary>
public static class TelegramGatewayOtpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TelegramGatewayOtpDeliveryHandler"/> for the <c>telegram-gateway</c> channel. Options come from
    /// <paramref name="configure"/>, then the host section <c>Identity:Otp:TelegramGateway</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Access token and optional sender.</param>
    public static IHttpClientBuilder AddTelegramGatewayOtpDelivery(this IServiceCollection services, Action<TelegramGatewayOtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            TelegramGatewayOtpOptions.SectionName,
            configure,
            builder => builder.Validate(o => !string.IsNullOrWhiteSpace(o.AccessToken), "TelegramGatewayOtpOptions.AccessToken must not be empty."));
        services.AddOtpDeliveryHandler<TelegramGatewayOtpDeliveryHandler>(OtpChannelNameConstants.TelegramGateway);
        return services.AddHttpClient(TelegramGatewayOtpDeliveryHandler.HttpClientName, (serviceProvider, http) =>
            http.BaseAddress = serviceProvider.GetRequiredService<TelegramGatewayOtpOptions>().BaseAddress);
    }
}
