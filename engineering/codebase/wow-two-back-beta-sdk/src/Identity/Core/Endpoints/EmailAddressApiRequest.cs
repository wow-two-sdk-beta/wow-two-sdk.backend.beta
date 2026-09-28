namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Names the account an email is sent to (confirmation resend, password reset).</summary>
public sealed record EmailAddressApiRequest
{
    /// <summary>The account email.</summary>
    public required string Email { get; init; }
}
