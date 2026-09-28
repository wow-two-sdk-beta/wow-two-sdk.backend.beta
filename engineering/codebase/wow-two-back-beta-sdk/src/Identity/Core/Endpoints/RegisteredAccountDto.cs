namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>The account a registration created.</summary>
public sealed record RegisteredAccountDto
{
    /// <summary>The new user id.</summary>
    public required string UserId { get; init; }

    /// <summary>Whether a confirmation email was sent.</summary>
    public bool ConfirmationSent { get; init; }
}
