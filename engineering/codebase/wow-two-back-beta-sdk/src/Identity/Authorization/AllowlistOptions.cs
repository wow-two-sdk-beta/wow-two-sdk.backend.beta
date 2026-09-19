namespace WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

/// <summary>Holds configuration for the principal allowlist; empty <see cref="Allowed"/> ⇒ OPEN (any authenticated principal passes, the single-admin default).</summary>
public sealed record AllowlistOptions
{
    /// <summary>Claim type the allowlist is keyed on; defaults to the normalized username claim from the <c>Identity/Claims</c> normalizer.</summary>
    // seam: Identity/Claims NormalizedClaimTypeConstants.Username
    public string ClaimType { get; set; } = "wt:username";

    /// <summary>Allowed claim values; empty (default) ⇒ OPEN (any authenticated principal passes).</summary>
    public ISet<string> Allowed { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Compare claim values case-insensitively. Default <c>true</c>.</summary>
    public bool CaseInsensitive { get; set; } = true;
}
