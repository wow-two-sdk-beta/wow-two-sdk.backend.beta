using System.Globalization;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Telegram;

/// <summary>
/// Delivers OTP codes as Telegram messages. <see cref="OtpDeliveryEnvelope.DeliveryAddress"/> must be
/// the numeric Telegram chat id — the consumer owns the bot-link flow that captures it.
/// The consumer must register <see cref="ITelegramBotClient"/> (the SDK doesn't take the bot token).
/// </summary>
public sealed class TelegramOtpDeliveryHandler : IOtpDeliveryHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramOtpOptions _telegramOptions;
    private readonly OtpOptions _otpOptions;

    /// <summary>Creates the handler over the consumer-registered bot client and options.</summary>
    /// <param name="botClient">Telegram bot client (consumer-registered, carries the token).</param>
    /// <param name="telegramOptions">Message template and scope display names.</param>
    /// <param name="otpOptions">Source of the code lifetime shown in the message.</param>
    public TelegramOtpDeliveryHandler(
        ITelegramBotClient botClient,
        IOptions<TelegramOtpOptions> telegramOptions,
        IOptions<OtpOptions> otpOptions)
    {
        ArgumentNullException.ThrowIfNull(botClient);
        ArgumentNullException.ThrowIfNull(telegramOptions);
        ArgumentNullException.ThrowIfNull(otpOptions);
        _botClient = botClient;
        _telegramOptions = telegramOptions.Value;
        _otpOptions = otpOptions.Value;
    }

    /// <inheritdoc />
    public async Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!long.TryParse(envelope.DeliveryAddress, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
        {
            return new OtpDeliveryResult(false, "invalid_chat_id");
        }

        var scopeName = _telegramOptions.ScopeDisplayNames.TryGetValue(envelope.Scope, out var display)
            ? display
            : envelope.Scope;
        var minutes = (int)Math.Ceiling(_otpOptions.CodeLifetime.TotalMinutes);
        var text = string.Format(CultureInfo.InvariantCulture, _telegramOptions.MessageTemplate, scopeName, envelope.Code, minutes);

        try
        {
            await _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new OtpDeliveryResult(true);
        }
        catch (RequestException exception)
        {
            return new OtpDeliveryResult(false, exception.Message);
        }
    }
}
