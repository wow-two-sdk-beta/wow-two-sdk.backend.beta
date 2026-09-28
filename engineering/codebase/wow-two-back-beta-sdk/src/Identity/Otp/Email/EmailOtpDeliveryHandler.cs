using System.Globalization;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Email;

/// <summary>Handles <see cref="OtpDeliveryEnvelopeModel"/> deliveries as plain-text email through the registered <see cref="IEmailBroker"/>.</summary>
/// <param name="email">The email provider.</param>
/// <param name="formatter">Words the subject and text the envelope does not carry.</param>
/// <param name="emailOptions">The sender.</param>
/// <param name="otpOptions">OTP settings, for the lifetime shown in the message.</param>
public sealed class EmailOtpDeliveryHandler(
    IEmailBroker email,
    IOtpMessageFormatter formatter,
    EmailOtpOptions emailOptions,
    OtpOptions otpOptions) : IOtpDeliveryHandler
{
    /// <inheritdoc />
    public async Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelopeModel envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.DeliveryAddress) || !envelope.DeliveryAddress.Contains('@', StringComparison.Ordinal))
            return new OtpDeliveryResult { Success = false, FailureReason = "invalid_email_address" };

        var worded = envelope.Text is null || envelope.Subject is null
            ? formatter.Format(envelope.Scope, OtpChannelNameConstants.Email, envelope.Code, envelope.Lifetime ?? otpOptions.CodeLifetime, CultureOf(envelope.Culture))
            : null;
        var message = EmailMessage.Create(envelope.DeliveryAddress, envelope.Subject ?? worded!.Subject, envelope.Text ?? worded!.Text) with
        {
            From = emailOptions.From is { Length: > 0 } from ? new EmailAddress { Address = from, DisplayName = emailOptions.FromName } : null,
        };

        var result = await email.SendAsync(message, cancellationToken);
        return new OtpDeliveryResult { Success = result.Success, FailureReason = result.FailureReason };
    }

    private static CultureInfo? CultureOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
