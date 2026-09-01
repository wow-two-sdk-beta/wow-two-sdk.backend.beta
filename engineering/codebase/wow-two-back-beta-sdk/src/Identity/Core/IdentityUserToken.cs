using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;
/// <summary>A token held for a user against one provider (composite key user + provider + name).</summary>
/// <typeparam name="TKey">User key type.</typeparam>
public class IdentityUserToken<TKey> : ICompositeKeyEntity
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The owning user.</summary>
    public TKey UserId { get; set; } = default!;

    /// <summary>Token provider (e.g. <c>Default</c>, <c>Authenticator</c>).</summary>
    public string LoginProvider { get; set; } = string.Empty;

    /// <summary>Token name (e.g. <c>ResetPassword</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Token value.</summary>
    public string? Value { get; set; }
}
