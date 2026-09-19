namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Holds options for <see cref="ClaimMapper"/>: avatar-synthesis toggle plus per-provider specs, seeded from <see cref="ClaimProviderSpecFactory.CreateDefault"/>.</summary>
public sealed record ClaimNormalizationOptions
{
    /// <summary>Honor a spec's avatar synthesizer when no ready avatar claim is present. Default <c>true</c>.</summary>
    public bool SynthesizeAvatars { get; set; } = true;

    /// <summary>Scheme → spec map (case-insensitive), pre-populated with every built-in provider.</summary>
    public IDictionary<string, ClaimProviderSpec> Specs { get; } = ClaimProviderSpecFactory.CreateDefault();

    /// <summary>Adds or replaces the spec for <paramref name="scheme"/>.</summary>
    /// <param name="scheme">Auth scheme name (matched case-insensitively).</param>
    /// <param name="spec">Provider spec for that scheme.</param>
    public ClaimNormalizationOptions AddProvider(string scheme, ClaimProviderSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentNullException.ThrowIfNull(spec);
        Specs[scheme] = spec;
        return this;
    }
}
