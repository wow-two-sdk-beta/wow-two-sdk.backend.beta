using System.Globalization;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Sms;

/// <summary>
/// Handles <see cref="OtpDeliveryEnvelopeModel"/> deliveries as text messages through the SMS broker the envelope or
/// <see cref="SmsOtpOptions.Broker"/> names, else the default broker; the delivery address is an E.164 number.
/// </summary>
/// <param name="brokers">Creates the SMS broker by name.</param>
/// <param name="smsOtpOptions">Broker choice and fallback wording.</param>
/// <param name="otpOptions">OTP settings, for the lifetime shown in fallback wording.</param>
public sealed class SmsOtpDeliveryHandler(ISmsBrokerFactory brokers, SmsOtpOptions smsOtpOptions, OtpOptions otpOptions) : IOtpDeliveryHandler
{
    /// <inheritdoc />
    public async Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelopeModel envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.DeliveryAddress))
            return new OtpDeliveryResult { Success = false, FailureReason = "invalid_phone_number" };

        var brokerName = envelope.Broker ?? smsOtpOptions.Broker;
        if (brokers.Create(brokerName) is not { } sms)
            return new OtpDeliveryResult { Success = false, FailureReason = $"sms_broker_not_registered: {brokerName}" };

        var body = envelope.Text ?? FallbackText(envelope);
        var result = await sms.SendAsync(new SmsMessage { To = envelope.DeliveryAddress, Body = body }, cancellationToken);
        return new OtpDeliveryResult { Success = result.Success, FailureReason = result.FailureReason };
    }

    private string FallbackText(OtpDeliveryEnvelopeModel envelope)
    {
        var scopeName = smsOtpOptions.ScopeDisplayNames.TryGetValue(envelope.Scope, out var display) ? display : envelope.Scope;
        var minutes = (int)Math.Ceiling((envelope.Lifetime ?? otpOptions.CodeLifetime).TotalMinutes);
        return string.Format(CultureInfo.InvariantCulture, smsOtpOptions.MessageTemplate, scopeName, envelope.Code, minutes);
    }
}
