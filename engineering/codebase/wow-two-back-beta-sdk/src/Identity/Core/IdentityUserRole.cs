using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Join row assigning a role to a user (composite key user + role). Consumed by the roles slice.</summary>
/// <typeparam name="TKey">User/role key type.</typeparam>
public class IdentityUserRole<TKey> : ICompositeKeyEntity
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The user.</summary>
    public TKey UserId { get; set; } = default!;

    /// <summary>The role.</summary>
    public TKey RoleId { get; set; } = default!;
}
