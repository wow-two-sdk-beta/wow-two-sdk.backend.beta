using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Join row assigning a role to a user (composite key user + role). Consumed by the roles slice.</summary>
/// <typeparam name="TKey">User/role key type.</typeparam>
public class IdentityUserRole<TKey> : IEntity
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The user.</summary>
    public TKey UserId { get; set; } = default!;

    /// <summary>The role.</summary>
    public TKey RoleId { get; set; } = default!;
}

/// <summary>A claim held by a user.</summary>
/// <typeparam name="TKey">User key type.</typeparam>
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

/// <summary>A claim granted to everyone in a role.</summary>
/// <typeparam name="TKey">Role key type.</typeparam>
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

/// <summary>An external login (Google/Microsoft/Telegram/…) linked to a user (composite key provider + provider-key).</summary>
/// <typeparam name="TKey">User key type.</typeparam>
public class IdentityUserLogin<TKey> : IEntity
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>External provider name (e.g. <c>Google</c>).</summary>
    public string LoginProvider { get; set; } = string.Empty;

    /// <summary>The user's id at the provider.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>Friendly provider name for display.</summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>The local user this login maps to.</summary>
    public TKey UserId { get; set; } = default!;
}

/// <summary>A persisted per-user token — reset / 2FA / recovery / provider tokens (composite key user + provider + name).</summary>
/// <typeparam name="TKey">User key type.</typeparam>
public class IdentityUserToken<TKey> : IEntity
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
