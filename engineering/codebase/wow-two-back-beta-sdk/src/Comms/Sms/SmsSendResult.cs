namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Represents the outcome of a send attempt.</summary>
public sealed record SmsSendResult
{
    /// <summary>Whether the provider accepted the message; delivery to the handset is reported later, if at all.</summary>
    public required bool Success { get; init; }

    /// <summary>Provider-assigned message id, when available.</summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>Provider-specific failure detail (present on failure).</summary>
    public string? FailureReason { get; init; }
}
