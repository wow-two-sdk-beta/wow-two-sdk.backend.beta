namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

/// <summary>An issued refresh token as the client receives it.</summary>
public sealed record RefreshTokenModel
{
    /// <summary>The opaque token: id and secret, URL-safe.</summary>
    public required string Token { get; init; }

    /// <summary>When the token stops being redeemable (UTC).</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
