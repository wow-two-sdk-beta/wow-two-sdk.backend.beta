using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Telegram;

/// <summary>Telegram OTP delivery registration.</summary>
public static class TelegramOtpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TelegramOtpDeliveryHandler"/> for the <c>telegram</c> channel (keyed, and additive for callers
    /// that enumerate handlers); requires a consumer-registered <c>ITelegramBotClient</c>. Options come from
    /// <paramref name="configure"/>, then the host section <c>Identity:Otp:Telegram</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional message template / scope display-name overrides.</param>
    public static IServiceCollection AddTelegramOtpDelivery(
        this IServiceCollection services,
        Action<TelegramOtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            TelegramOtpOptions.SectionName,
            configure,
            builder => builder.Validate(
                options => !string.IsNullOrWhiteSpace(options.MessageTemplate),
                "TelegramOtpOptions.MessageTemplate must not be empty."));

        services.AddOtpDeliveryHandler<TelegramOtpDeliveryHandler>(OtpChannelNameConstants.Telegram);
        return services;
    }
}
