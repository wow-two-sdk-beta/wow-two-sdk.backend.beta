namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Holds the names delivery handlers register under; a method or caller names one to pick its channel.</summary>
public static class OtpChannelNameConstants
{
    /// <summary>Text message through an <c>ISmsBroker</c>.</summary>
    public const string Sms = "sms";

    /// <summary>WhatsApp message through an <c>IWhatsAppBroker</c>.</summary>
    public const string WhatsApp = "whatsapp";

    /// <summary>Telegram bot message to a linked chat.</summary>
    public const string Telegram = "telegram";

    /// <summary>Telegram Gateway verification message to a phone number.</summary>
    public const string TelegramGateway = "telegram-gateway";

    /// <summary>Email through an <c>IEmailBroker</c>.</summary>
    public const string Email = "email";
}
