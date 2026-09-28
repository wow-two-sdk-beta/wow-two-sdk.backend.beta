namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Vonage;

/// <summary>Holds Vonage SMS API credentials.</summary>
public sealed record VonageSmsOptions
{
    /// <summary>API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API secret.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>API base address. Default <c>https://rest.nexmo.com/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://rest.nexmo.com/");
}
