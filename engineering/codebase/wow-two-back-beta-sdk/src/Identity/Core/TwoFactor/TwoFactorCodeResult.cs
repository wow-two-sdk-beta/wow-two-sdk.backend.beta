namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Represents the outcome of sending a two-factor code.</summary>
public sealed record TwoFactorCodeResult
{
    /// <summary>The outcome.</summary>
    public required TwoFactorCodeStatus Status { get; init; }

    /// <summary>The method the code was for.</summary>
    public required string Method { get; init; }

    /// <summary>When the code stops verifying; set when <see cref="Status"/> is <see cref="TwoFactorCodeStatus.Sent"/>.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>The channel's failure detail; set for <see cref="TwoFactorCodeStatus.DeliveryFailed"/>.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Whether the code went out.</summary>
    public bool Sent => Status == TwoFactorCodeStatus.Sent;
}
