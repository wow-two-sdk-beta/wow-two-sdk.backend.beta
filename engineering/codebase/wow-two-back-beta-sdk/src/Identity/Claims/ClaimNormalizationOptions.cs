namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Options for <see cref="ClaimNormalizer"/>: avatar-synthesis toggle plus per-provider profiles, seeded from <see cref="ClaimProviderProfiles.CreateDefault"/>.</summary>
public sealed record ClaimNormalizationOptions
{
    /// <summary>Honor a profile's avatar synthesizer when no ready avatar claim is present. Default <c>true</c>.</summary>
    public bool SynthesizeAvatars { get; init; } = true;

    /// <summary>Scheme → profile map (case-insensitive), pre-populated with every built-in provider.</summary>
    public IDictionary<string, ClaimProviderProfile> Profiles { get; } = ClaimProviderProfiles.CreateDefault();

    /// <summary>Adds or replaces the profile for <paramref name="scheme"/>.</summary>
    /// <param name="scheme">Auth scheme name (matched case-insensitively).</param>
    /// <param name="profile">Provider profile for that scheme.</param>
    public ClaimNormalizationOptions AddProvider(string scheme, ClaimProviderProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentNullException.ThrowIfNull(profile);
        Profiles[scheme] = profile;
        return this;
    }
}
