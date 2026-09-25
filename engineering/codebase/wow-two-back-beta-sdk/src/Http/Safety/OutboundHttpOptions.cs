namespace WoW.Two.Sdk.Backend.Beta.Http.Safety;

/// <summary>Holds destination restrictions for a directly connected outbound HTTP client.</summary>
public sealed record OutboundHttpOptions
{
    /// <summary>Gets or sets whether destinations must use HTTPS. Defaults to true.</summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>Gets or sets whether trusted internal destinations may use private addresses.</summary>
    public bool AllowPrivateNetworkTargets { get; set; }

    /// <summary>Gets the exact DNS hosts permitted; empty permits any public destination.</summary>
    public ISet<string> AllowedHosts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
