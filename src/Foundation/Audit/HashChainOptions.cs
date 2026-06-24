namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Configuration for the hash-chain sealer and verifier.</summary>
public sealed class HashChainOptions
{
    /// <summary>Gets or sets the hash function used to seal and verify the chain. Default <see cref="HashChainAlgorithm.Sha256"/>.</summary>
    public HashChainAlgorithm Algorithm { get; set; } = HashChainAlgorithm.Sha256;
}
