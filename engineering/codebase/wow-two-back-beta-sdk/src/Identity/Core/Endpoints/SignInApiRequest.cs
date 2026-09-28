namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Signs in with a login and password, and a second factor when the account requires one.</summary>
public sealed record SignInApiRequest
{
    /// <summary>The user name or email.</summary>
    public required string Login { get; init; }

    /// <summary>The password.</summary>
    public required string Password { get; init; }

    /// <summary>A second-factor code, when two-factor is enabled: from the authenticator or the delivered method.</summary>
    public string? TwoFactorCode { get; init; }

    /// <summary>
    /// The second-factor method, such as <c>sms</c>; null takes the preferred method. Naming a delivered method without a
    /// code sends that method's code.
    /// </summary>
    public string? TwoFactorMethod { get; init; }

    /// <summary>A recovery code, instead of an authenticator code.</summary>
    public string? RecoveryCode { get; init; }
}
