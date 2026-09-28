namespace WoW.Two.Sdk.Backend.Beta.Comms.Push;

/// <summary>Represents the outcome of a push attempt.</summary>
public sealed record PushSendResult
{
    /// <summary>Whether the provider accepted the notification.</summary>
    public required bool Success { get; init; }

    /// <summary>Provider-assigned message id, when available.</summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>Provider-specific failure detail (present on failure).</summary>
    public string? FailureReason { get; init; }

    /// <summary>The provider reports the device token as unregistered or malformed; stop sending to it.</summary>
    public bool TokenInvalid { get; init; }
}
