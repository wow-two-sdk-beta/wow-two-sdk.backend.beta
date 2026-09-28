namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>The account's two-factor state.</summary>
public sealed record TwoFactorStatusDto
{
    /// <summary>Whether sign-in requires a second factor.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Whether an authenticator key is set up.</summary>
    public required bool HasAuthenticator { get; init; }

    /// <summary>Unused recovery codes.</summary>
    public required int RecoveryCodesLeft { get; init; }

    /// <summary>The methods the account can use: <c>authenticator</c> once set up, and each delivered method with a confirmed address.</summary>
    public IReadOnlyList<string> Methods { get; init; } = [];

    /// <summary>The method a sign-in asks for first.</summary>
    public string? PreferredMethod { get; init; }
}
