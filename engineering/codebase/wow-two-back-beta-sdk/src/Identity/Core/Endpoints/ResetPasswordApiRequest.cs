namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Resets a forgotten password with the emailed token.</summary>
public sealed record ResetPasswordApiRequest
{
    /// <summary>The account email.</summary>
    public required string Email { get; init; }

    /// <summary>The token from the reset email.</summary>
    public required string ResetToken { get; init; }

    /// <summary>The replacement password.</summary>
    public required string NewPassword { get; init; }
}
