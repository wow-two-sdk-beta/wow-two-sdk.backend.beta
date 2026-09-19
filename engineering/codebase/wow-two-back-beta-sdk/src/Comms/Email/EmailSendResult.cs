namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>Represents outcome of a send attempt.</summary>
public sealed record EmailSendResult
{
    /// <summary>Whether the provider accepted the message.</summary>
    public required bool Success { get; init; }

    /// <summary>Provider-assigned message id, when available.</summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>Provider-specific failure detail (present on failure).</summary>
    public string? FailureReason { get; init; }
}
