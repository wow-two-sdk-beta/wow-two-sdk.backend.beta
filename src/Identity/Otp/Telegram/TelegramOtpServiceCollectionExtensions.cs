using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Telegram;

/// <summary>Telegram OTP delivery registration.</summary>
public static class TelegramOtpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TelegramOtpDeliveryHandler"/> as an <see cref="IOtpDeliveryHandler"/>.
    /// Requires a consumer-registered <c>ITelegramBotClient</c> (e.g.
    /// <c>services.AddSingleton&lt;ITelegramBotClient&gt;(_ =&gt; new TelegramBotClient(token))</c>).
    /// Other channels can register alongside — handlers are additive.
    /// </summary>
    /// <param name="services">The service collection.</param>
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
        }

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOtpDeliveryHandler, TelegramOtpDeliveryHandler>());
        return services;
    }
}
