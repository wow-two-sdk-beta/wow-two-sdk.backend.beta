namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Key-size constants shared across the envelope-cryptography module.</summary>
internal static class KeySizes
{
    /// <summary>256-bit symmetric key length in bytes — the size of both the master key (KEK) and every data key (DEK).</summary>
    public const int SymmetricKeyBytes = 32;
}
