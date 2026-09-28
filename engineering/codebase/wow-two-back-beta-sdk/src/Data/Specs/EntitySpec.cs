namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>
/// Represents one entity's storage shape — table, key, columns, indexes, concurrency, soft delete and tenant — in terms
/// every mapper reads: EF Core today, Dapper and others through their own mappers.
/// </summary>
public sealed record EntitySpec
{
    /// <summary>The entity type.</summary>
    public required Type EntityType { get; init; }

    /// <summary>The table; null lets the mapper decide.</summary>
    public string? Table { get; init; }

    /// <summary>The schema; null takes the store's default.</summary>
    public string? Schema { get; init; }

    /// <summary>The key properties, in order; empty lets the mapper infer the key.</summary>
    public IReadOnlyList<string> Key { get; init; } = [];

    /// <summary>The configured properties by name; unlisted properties follow the mapper's conventions.</summary>
    public IReadOnlyDictionary<string, PropertySpec> Properties { get; init; } = new Dictionary<string, PropertySpec>(StringComparer.Ordinal);

    /// <summary>The indexes.</summary>
    public IReadOnlyList<IndexSpec> Indexes { get; init; } = [];

    /// <summary>The concurrency token, when the entity has one.</summary>
    public ConcurrencySpec? Concurrency { get; init; }

    /// <summary>The boolean property that marks a row deleted; reads leave such rows out.</summary>
    public string? SoftDeleteProperty { get; init; }

    /// <summary>The property holding the owning tenant.</summary>
    public string? TenantProperty { get; init; }
}
