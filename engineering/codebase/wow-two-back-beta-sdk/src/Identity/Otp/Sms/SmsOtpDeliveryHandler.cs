using System.Globalization;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Sms;

/// <summary>Delivers OTP codes as text messages through the registered <see cref="ISmsBroker"/>; the delivery address is an E.164 number.</summary>
/// <param name="sms">The SMS provider.</param>
/// <param name="smsOtpOptions">Message template and scope names.</param>
/// <param name="otpOptions">OTP settings, for the lifetime shown in the message.</param>
public sealed class SmsOtpDeliveryHandler(ISmsBroker sms, SmsOtpOptions smsOtpOptions, OtpOptions otpOptions) : IOtpDeliveryHandler
{
    /// <inheritdoc />
    public async Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelopeModel envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.DeliveryAddress))
            return new OtpDeliveryResult { Success = false, FailureReason = "invalid_phone_number" };

        var scopeName = smsOtpOptions.ScopeDisplayNames.TryGetValue(envelope.Scope, out var display) ? display : envelope.Scope;
        var minutes = (int)Math.Ceiling(otpOptions.CodeLifetime.TotalMinutes);
        var body = string.Format(CultureInfo.InvariantCulture, smsOtpOptions.MessageTemplate, scopeName, envelope.Code, minutes);

        var result = await sms.SendAsync(new SmsMessage { To = envelope.DeliveryAddress, Body = body }, cancellationToken);
        return new OtpDeliveryResult { Success = result.Success, FailureReason = result.FailureReason };
    }
}
