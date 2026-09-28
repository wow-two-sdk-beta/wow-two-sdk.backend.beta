namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Signs in with a login and password, and a second factor when the account requires one.</summary>
public sealed record SignInApiRequest
{
    /// <summary>The user name or email.</summary>
    public required string Login { get; init; }

    /// <summary>The password.</summary>
    public required string Password { get; init; }

    /// <summary>An authenticator code, when two-factor is enabled.</summary>
    public string? TwoFactorCode { get; init; }

    /// <summary>A recovery code, instead of an authenticator code.</summary>
    public string? RecoveryCode { get; init; }
}
