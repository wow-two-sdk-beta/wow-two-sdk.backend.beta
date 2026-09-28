namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;

/// <summary>Holds the relying party a passkey is bound to and how ceremonies run.</summary>
/// <remarks>Set in code with <c>.AddPasskeys(o => …)</c> or in the host section <c>Identity:Passkeys</c>, which is applied last.</remarks>
public sealed record PasskeyOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Identity:Passkeys";

    /// <summary>Gets or sets the relying-party id — the site's registrable domain, such as <c>acme.com</c>.</summary>
    public string ServerDomain { get; set; } = string.Empty;

    /// <summary>Gets or sets the name authenticators show.</summary>
    public string ServerName { get; set; } = "App";

    /// <summary>Gets the origins ceremonies may come from, such as <c>https://acme.com</c>.</summary>
    public List<string> Origins { get; } = [];

    /// <summary>Gets or sets how long a started ceremony stays completable. Default 5 minutes.</summary>
    public TimeSpan CeremonyLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets whether the authenticator must verify the user (PIN, biometrics); off prefers it. Default false.</summary>
    public bool RequireUserVerification { get; set; }
}
