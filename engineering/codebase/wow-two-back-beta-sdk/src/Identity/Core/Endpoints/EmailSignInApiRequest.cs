namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Signs in with the token of an emailed link or an emailed code.</summary>
public sealed record EmailSignInApiRequest
{
    /// <summary>The address the link or code went to.</summary>
    public required string Email { get; init; }

    /// <summary>The token from the link.</summary>
    public string? Token { get; init; }

    /// <summary>The code, when no token is given.</summary>
    public string? Code { get; init; }
}
