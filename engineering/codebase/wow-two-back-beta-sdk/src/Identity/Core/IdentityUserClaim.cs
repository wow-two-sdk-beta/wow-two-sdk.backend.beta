using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;
/// <summary>A claim held by a user.</summary>
/// <typeparam name="TKey">Role key type.</typeparam>
public class IdentityUserClaim<TKey> : IKeyedEntity<int>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Surrogate primary key (store-generated).</summary>
    public int Id { get; set; }

    /// <summary>The owning user.</summary>
    public TKey UserId { get; set; } = default!;

    /// <summary>Claim type (URI or short name).</summary>
    public string? ClaimType { get; set; }

    /// <summary>Claim value.</summary>
    public string? ClaimValue { get; set; }
}
