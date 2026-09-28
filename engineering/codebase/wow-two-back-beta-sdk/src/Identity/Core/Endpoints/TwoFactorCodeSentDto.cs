namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>A delivered two-factor code went out.</summary>
public sealed record TwoFactorCodeSentDto
{
    /// <summary>The method the code was sent by.</summary>
    public required string Method { get; init; }

    /// <summary>When the code stops verifying.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
