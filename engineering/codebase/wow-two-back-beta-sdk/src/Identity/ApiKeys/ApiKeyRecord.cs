namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Represents a live API key as a store answers a lookup — enough to authenticate and to record use.</summary>
/// <param name="Id">The key's identifier, as the store names it.</param>
/// <param name="Name">The name its owner gave the key, carried as the principal's name.</param>
/// <param name="LastUsedAt">When the key last authenticated a request, or <c>null</c> before its first use.</param>
public sealed record ApiKeyRecord(string Id, string Name, DateTimeOffset? LastUsedAt);
