namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Represents an index over one or more properties.</summary>
public sealed record IndexSpec
{
    /// <summary>The indexed properties, in order.</summary>
    public required IReadOnlyList<string> Properties { get; init; }

    /// <summary>Whether the index rejects duplicates.</summary>
    public bool IsUnique { get; init; }

    /// <summary>The index name; null lets the mapper name it.</summary>
    public string? Name { get; init; }

    /// <summary>A partial-index predicate in the store's SQL.</summary>
    public string? Filter { get; init; }
}
