using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates a Telegram bot update: <c>X-Telegram-Bot-Api-Secret-Token</c> must equal a configured secret.</summary>
/// <remarks>
/// The secret is the <c>secret_token</c> given to <c>setWebhook</c>; Telegram signs nothing, so the body is not covered.
/// The delivery id is the update's <c>update_id</c> and the event type its kind, such as <c>message</c>.
/// </remarks>
public sealed class TelegramWebhookSignatureValidator : IWebhookSignatureValidator
{
    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        string? token = headers["X-Telegram-Bot-Api-Secret-Token"];
        if (string.IsNullOrEmpty(token))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);

        var presented = token.Utf8();
        if (!receiver.Secrets.Any(secret => CryptographicOperations.FixedTimeEquals(secret.Utf8(), presented)))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);

        return new WebhookSignatureResult { DeliveryId = body.JsonProperty("update_id"), EventType = body.FirstJsonPropertyExcept("update_id") };
    }
}
