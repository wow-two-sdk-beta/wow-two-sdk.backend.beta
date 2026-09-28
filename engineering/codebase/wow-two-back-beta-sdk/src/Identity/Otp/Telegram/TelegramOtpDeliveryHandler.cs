using System.Globalization;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Telegram;

/// <summary>Handles <see cref="OtpDeliveryEnvelopeModel"/> deliveries as Telegram bot messages; the delivery address is the chat id.</summary>
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
        TelegramOtpOptions telegramOptions,
        OtpOptions otpOptions)
    {
        ArgumentNullException.ThrowIfNull(botClient);
        ArgumentNullException.ThrowIfNull(telegramOptions);
        ArgumentNullException.ThrowIfNull(otpOptions);
        _botClient = botClient;
        _telegramOptions = telegramOptions;
        _otpOptions = otpOptions;
    }

    /// <inheritdoc />
    public async Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelopeModel envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!long.TryParse(envelope.DeliveryAddress, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
        {
            return new OtpDeliveryResult { Success = false, FailureReason = "invalid_chat_id" };
        }

        var text = envelope.Text ?? FallbackText(envelope);

        try
        {
            await _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new OtpDeliveryResult { Success = true };
        }
        catch (RequestException exception)
        {
            return new OtpDeliveryResult { Success = false, FailureReason = exception.Message };
        }
    }

    private string FallbackText(OtpDeliveryEnvelopeModel envelope)
    {
        var scopeName = _telegramOptions.ScopeDisplayNames.TryGetValue(envelope.Scope, out var display)
            ? display
            : envelope.Scope;
        var minutes = (int)Math.Ceiling((envelope.Lifetime ?? _otpOptions.CodeLifetime).TotalMinutes);
        return string.Format(CultureInfo.InvariantCulture, _telegramOptions.MessageTemplate, scopeName, envelope.Code, minutes);
    }
}
