namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Holds the names WhatsApp brokers register under, and the host configuration sections they read.</summary>
public static class WhatsAppBrokerNameConstants
{
    /// <summary>Meta's WhatsApp Cloud API; section <c>Comms:WhatsApp:Meta</c>.</summary>
    public const string Meta = "meta";

    /// <summary>Twilio's WhatsApp sender; section <c>Comms:WhatsApp:Twilio</c>.</summary>
    public const string Twilio = "twilio";
}
