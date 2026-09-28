namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Represents a new API key secret with what a repository keeps of it.</summary>
public sealed record ApiKeySecret
{
    /// <summary>The secret, shown to its owner once and never stored.</summary>
    public required string Secret { get; init; }

    /// <summary>The display prefix — the marker and the first random characters — to tell keys apart.</summary>
    public required string Prefix { get; init; }

    /// <summary>The lowercase hex SHA-256 of the secret, the only form a repository keeps.</summary>
    public required string Hash { get; init; }
}
