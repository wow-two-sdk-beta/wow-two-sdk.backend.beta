namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;

/// <summary>Represents supplied data for SmsPayload encoding.</summary>
public sealed record SmsPayloadModel
{
    /// <summary>Gets the Phone value.</summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>Gets the Message value.</summary>
    public string? Message { get; init; }
}
