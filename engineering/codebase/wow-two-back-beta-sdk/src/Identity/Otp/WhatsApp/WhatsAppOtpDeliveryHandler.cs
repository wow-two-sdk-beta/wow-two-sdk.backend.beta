using System.Globalization;
using WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.WhatsApp;

/// <summary>
/// Handles <see cref="OtpDeliveryEnvelopeModel"/> deliveries as WhatsApp messages through the broker the envelope or
/// <see cref="WhatsAppOtpOptions.Broker"/> names: the configured authentication template, else free text.
/// </summary>
/// <param name="brokers">Creates the WhatsApp broker by name.</param>
/// <param name="formatter">Words the free-text fallback.</param>
/// <param name="whatsAppOptions">Broker choice and template.</param>
/// <param name="otpOptions">OTP settings, for the lifetime shown in free text.</param>
public sealed class WhatsAppOtpDeliveryHandler(
    IWhatsAppBrokerFactory brokers,
    IOtpMessageFormatter formatter,
    WhatsAppOtpOptions whatsAppOptions,
    OtpOptions otpOptions) : IOtpDeliveryHandler
{
    /// <inheritdoc />
    public async Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelopeModel envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.DeliveryAddress))
            return new OtpDeliveryResult { Success = false, FailureReason = "invalid_phone_number" };

        var brokerName = envelope.Broker ?? whatsAppOptions.Broker;
        if (brokers.Create(brokerName) is not { } whatsApp)
            return new OtpDeliveryResult { Success = false, FailureReason = $"whatsapp_broker_not_registered: {brokerName}" };

        var message = whatsAppOptions.TemplateName is { Length: > 0 } template
            ? new WhatsAppMessage
            {
                To = envelope.DeliveryAddress,
                Template = new WhatsAppTemplate
                {
                    Name = template,
                    LanguageCode = LanguageOf(envelope.Culture),
                    BodyParameters = [envelope.Code],
                    ButtonParameter = whatsAppOptions.CopyCodeButton ? envelope.Code : null,
                },
            }
            : new WhatsAppMessage { To = envelope.DeliveryAddress, Text = envelope.Text ?? FallbackText(envelope) };

        var result = await whatsApp.SendAsync(message, cancellationToken);
        return new OtpDeliveryResult { Success = result.Success, FailureReason = result.FailureReason };
    }

    private string LanguageOf(string? culture)
    {
        for (var current = CultureOf(culture); current is not null && !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            if (whatsAppOptions.TemplateLanguages.TryGetValue(current.Name, out var language))
                return language;
        }

        return whatsAppOptions.TemplateLanguage;
    }

    private string FallbackText(OtpDeliveryEnvelopeModel envelope)
        => formatter.Format(envelope.Scope, OtpChannelNameConstants.WhatsApp, envelope.Code, envelope.Lifetime ?? otpOptions.CodeLifetime, CultureOf(envelope.Culture)).Text;

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
