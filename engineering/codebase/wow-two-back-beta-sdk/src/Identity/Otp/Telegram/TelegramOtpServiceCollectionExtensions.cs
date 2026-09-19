using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

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

        services.AddValidatedOptions<TelegramOtpOptions>(
            configure,
            builder => builder.Validate(
                options => !string.IsNullOrWhiteSpace(options.MessageTemplate),
                "TelegramOtpOptions.MessageTemplate must not be empty."));

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOtpDeliveryHandler, TelegramOtpDeliveryHandler>());
        return services;
    }
}
