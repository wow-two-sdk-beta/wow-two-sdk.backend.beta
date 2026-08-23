using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Telegram;

/// <summary>Telegram OTP delivery registration.</summary>
public static class TelegramOtpServiceCollectionExtensions
{
    /// <summary>Registers <see cref="TelegramOtpDeliveryHandler"/> as an additive <see cref="IOtpDeliveryHandler"/>; requires a consumer-registered <c>ITelegramBotClient</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional message template / scope display-name overrides.</param>
    public static IServiceCollection AddTelegramOtpDelivery(
        this IServiceCollection services,
        Action<TelegramOtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<TelegramOtpOptions>();
            services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<TelegramOtpOptions>>().Value);
        }

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOtpDeliveryHandler, TelegramOtpDeliveryHandler>());
        return services;
    }
}
