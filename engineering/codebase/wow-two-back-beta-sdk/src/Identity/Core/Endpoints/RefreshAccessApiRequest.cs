namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Exchanges a refresh token for a new access token and refresh token.</summary>
public sealed record RefreshAccessApiRequest
{
    /// <summary>The refresh token from the last sign-in or refresh.</summary>
    public required string RefreshToken { get; init; }
}
