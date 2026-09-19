using WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google.Authenticators;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;

/// <summary>The verified claims from a Google ID token — the trusted output of <see cref="IGoogleIdTokenAuthenticator"/>.</summary>
public sealed record GoogleVerifiedIdentity
{
    /// <summary>Google's stable per-account identifier (the <c>sub</c> claim).</summary>
    public required string Subject { get; init; }

    /// <summary>The account's verified email address.</summary>
    public required string Email { get; init; }

    /// <summary>The account's display name.</summary>
    public required string Name { get; init; }

    /// <summary>The account's avatar URL, when present.</summary>
    public required string? Picture { get; init; }
}
