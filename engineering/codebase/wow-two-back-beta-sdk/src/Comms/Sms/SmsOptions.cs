namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Holds the cross-provider SMS defaults.</summary>
public sealed record SmsOptions
{
    /// <summary>Sender id or number used when a message names none; null leaves the provider's default.</summary>
    public string? DefaultFrom { get; set; }
}
