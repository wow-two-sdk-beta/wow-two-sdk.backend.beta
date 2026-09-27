namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Represents a new API key secret with what a store keeps of it.</summary>
/// <param name="Secret">The secret, shown to its owner once and never stored.</param>
/// <param name="Prefix">The display prefix — the marker and the first random characters — to tell keys apart.</param>
/// <param name="Hash">The lowercase hex SHA-256 of the secret, the only form a store keeps.</param>
public sealed record ApiKeySecret(string Secret, string Prefix, string Hash);
