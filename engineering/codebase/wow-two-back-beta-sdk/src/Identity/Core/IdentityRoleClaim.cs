using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;
/// <summary>A claim carried by a role, granted to every user in it.</summary>
/// <typeparam name="TKey">User key type.</typeparam>
public class IdentityRoleClaim<TKey> : IKeyedEntity<int>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Surrogate primary key (store-generated).</summary>
    public int Id { get; set; }

    /// <summary>The owning role.</summary>
    public TKey RoleId { get; set; } = default!;

    /// <summary>Claim type (URI or short name).</summary>
    public string? ClaimType { get; set; }

    /// <summary>Claim value.</summary>
    public string? ClaimValue { get; set; }
}
