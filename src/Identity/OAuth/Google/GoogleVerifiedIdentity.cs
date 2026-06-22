namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;

/// <summary>The verified claims from a Google ID token — the trusted output of <see cref="IGoogleIdTokenVerifier"/>.</summary>
/// <param name="Subject">Google's stable per-account identifier (the <c>sub</c> claim).</param>
/// <param name="Email">The account's verified email address.</param>
/// <param name="Name">The account's display name.</param>
/// <param name="Picture">The account's avatar URL, when present.</param>
public sealed record GoogleVerifiedIdentity(string Subject, string Email, string Name, string? Picture);
