namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Defines the cryptographic hash function used to seal and verify a chain.</summary>
/// <remarks>SHA-256 is the default and matches the vault's chain semantics. The value is part of the on-disk proof's interpretation — changing it for an existing chain invalidates every prior hash, so pick once per chain.</remarks>
public enum HashChainAlgorithm
{
    /// <summary>SHA-256 — 32-byte digest, the default.</summary>
    Sha256 = 0,

    /// <summary>SHA-384 — 48-byte digest.</summary>
    Sha384,

    /// <summary>SHA-512 — 64-byte digest.</summary>
    Sha512,
}
