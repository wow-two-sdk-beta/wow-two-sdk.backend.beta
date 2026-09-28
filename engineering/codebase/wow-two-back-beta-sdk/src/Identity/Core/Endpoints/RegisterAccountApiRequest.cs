namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Registers an account with an email and password.</summary>
public sealed record RegisterAccountApiRequest
{
    /// <summary>The account email.</summary>
    public required string Email { get; init; }

    /// <summary>The initial password.</summary>
    public required string Password { get; init; }

    /// <summary>Optional login name; the email when omitted.</summary>
    public string? UserName { get; init; }
}
