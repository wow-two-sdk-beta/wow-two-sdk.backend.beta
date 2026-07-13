namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Metadata for a resolved tenant — its stable id plus optional display name and arbitrary items.</summary>
public sealed record TenantInfo
{
    /// <summary>Gets the stable tenant identifier (matches the value stamped on <c>IHasTenant&lt;string&gt;</c> rows).</summary>
    public required string Id { get; init; }

    /// <summary>Gets an optional human-readable tenant name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets optional arbitrary per-tenant metadata (connection string name, tier, …).</summary>
    public IReadOnlyDictionary<string, string>? Items { get; init; }
}
