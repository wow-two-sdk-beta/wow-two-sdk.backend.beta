namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Represents a live API key as a repository answers a lookup — enough to authenticate and to record use.</summary>
public sealed record ApiKeyRecord
{
    /// <summary>The key's identifier, as the repository names it.</summary>
    public required string Id { get; init; }

    /// <summary>The name its owner gave the key, carried as the principal's name.</summary>
    public required string Name { get; init; }

    /// <summary>When the key last authenticated a request, or <c>null</c> before its first use.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
