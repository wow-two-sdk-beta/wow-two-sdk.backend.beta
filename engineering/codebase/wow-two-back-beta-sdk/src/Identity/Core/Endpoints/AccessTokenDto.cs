namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>The bearer session a sign-in or refresh returns.</summary>
public sealed record AccessTokenDto
{
    /// <summary>Always <c>Bearer</c>.</summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>The signed access token (JWT).</summary>
    public required string AccessToken { get; init; }

    /// <summary>Seconds until the access token expires.</summary>
    public required int ExpiresIn { get; init; }

    /// <summary>The rotating refresh token; null when refresh tokens are not registered.</summary>
    public string? RefreshToken { get; init; }
}
