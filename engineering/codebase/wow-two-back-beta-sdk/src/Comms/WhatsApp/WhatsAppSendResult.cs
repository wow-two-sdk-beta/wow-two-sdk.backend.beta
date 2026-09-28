namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Represents the outcome of a WhatsApp send attempt.</summary>
public sealed record WhatsAppSendResult
{
    /// <summary>Whether the provider accepted the message; delivery is reported later through webhooks, if at all.</summary>
    public required bool Success { get; init; }

    /// <summary>Provider-assigned message id (<c>wamid.…</c>, <c>SM…</c>), when available.</summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>Provider-specific failure detail (present on failure).</summary>
    public string? FailureReason { get; init; }
}
