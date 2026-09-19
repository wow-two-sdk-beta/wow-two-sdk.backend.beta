namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;

/// <summary>Represents supplied data for MailPayload encoding.</summary>
public sealed record MailPayloadModel
{
    /// <summary>Gets the Recipient value.</summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>Gets the Subject value.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the Body value.</summary>
    public string? Body { get; init; }
}
