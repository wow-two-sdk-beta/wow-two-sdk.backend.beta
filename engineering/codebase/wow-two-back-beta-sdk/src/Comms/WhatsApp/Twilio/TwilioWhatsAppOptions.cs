namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Twilio;

/// <summary>Holds Twilio credentials and the WhatsApp sender.</summary>
public sealed record TwilioWhatsAppOptions
{
    /// <summary>Gets or sets the account SID (<c>AC…</c>).</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Gets or sets the auth token for the account.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the WhatsApp-enabled sender number in E.164.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Gets or sets the API base address. Default <c>https://api.twilio.com/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://api.twilio.com/");
}
