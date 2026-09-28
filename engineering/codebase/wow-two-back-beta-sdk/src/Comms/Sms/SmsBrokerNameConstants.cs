namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Holds the names SMS brokers register under, and the host configuration sections they read.</summary>
public static class SmsBrokerNameConstants
{
    /// <summary>Twilio Messages API; section <c>Comms:Sms:Twilio</c>.</summary>
    public const string Twilio = "twilio";

    /// <summary>Vonage SMS API; section <c>Comms:Sms:Vonage</c>.</summary>
    public const string Vonage = "vonage";

    /// <summary>Eskiz (Uzbekistan); section <c>Comms:Sms:Eskiz</c>.</summary>
    public const string Eskiz = "eskiz";
}
