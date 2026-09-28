namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Twilio;

/// <summary>Holds Twilio credentials and the sender.</summary>
public sealed record TwilioSmsOptions
{
    /// <summary>Account SID (<c>AC…</c>).</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Auth token for the account.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Messaging service SID (<c>MG…</c>); when set it sends instead of a <c>From</c> number.</summary>
    public string? MessagingServiceSid { get; set; }

    /// <summary>API base address. Default <c>https://api.twilio.com/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://api.twilio.com/");
}
