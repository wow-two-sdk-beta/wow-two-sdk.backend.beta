namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>Outcome of a send attempt.</summary>
/// <param name="Success">Whether the provider accepted the message.</param>
/// <param name="ProviderMessageId">Provider-assigned message id, when available.</param>
/// <param name="FailureReason">Provider-specific failure detail (present on failure).</param>
public sealed record EmailSendResult(bool Success, string? ProviderMessageId = null, string? FailureReason = null);
