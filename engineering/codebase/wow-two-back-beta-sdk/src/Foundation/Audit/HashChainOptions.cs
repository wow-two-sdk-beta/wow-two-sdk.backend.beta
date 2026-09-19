namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Holds configuration for the hash-chain sealer and validator.</summary>
public sealed record HashChainOptions
{
    /// <summary>Gets or sets the hash function used to seal and validate the chain. Default <see cref="HashChainAlgorithm.Sha256"/>.</summary>
    public HashChainAlgorithm Algorithm { get; set; } = HashChainAlgorithm.Sha256;
}
