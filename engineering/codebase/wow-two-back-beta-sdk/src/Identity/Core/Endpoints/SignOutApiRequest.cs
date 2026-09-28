namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Ends a session; a bearer client passes its refresh token to revoke it.</summary>
public sealed record SignOutApiRequest
{
    /// <summary>The refresh token to revoke, if any.</summary>
    public string? RefreshToken { get; init; }
}
