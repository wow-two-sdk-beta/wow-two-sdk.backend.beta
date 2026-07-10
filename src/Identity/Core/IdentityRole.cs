using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>A role, with ASP.NET-Identity-shaped columns. Consumed by the roles slice; the core schema maps it so later slices need no migration.</summary>
/// <typeparam name="TKey">Primary-key type (default <see cref="Guid"/>).</typeparam>
public class IdentityRole<TKey> : IKeyedEntity<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Primary key. Store-generated for <see cref="Guid"/> keys.</summary>
    public TKey Id { get; set; } = default!;

    /// <summary>Role name.</summary>
    public string? Name { get; set; }

    /// <summary>Upper-invariant <see cref="Name"/> for case-insensitive unique lookup.</summary>
    public string? NormalizedName { get; set; }

    /// <summary>Value that changes on every persist — optimistic-concurrency guard.</summary>
    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    /// <summary>When the row was created (UTC) — stamped by the audit interceptor.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the row was last modified (UTC) — stamped by the audit interceptor.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A role keyed by <see cref="Guid"/> — the default shape.</summary>
public class IdentityRole : IdentityRole<Guid>;
