using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>
/// One issued refresh token, stored by digest. Tokens rotate within a family: redeeming one consumes it and issues the
/// next; presenting a consumed token again revokes the whole family. Consumed by the refresh-token slice.
/// </summary>
/// <typeparam name="TKey">User key type.</typeparam>
public class IdentityRefreshToken<TKey> : IKeyedEntity<Guid>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Primary key; the public half of the token.</summary>
    public Guid Id { get; set; }

    /// <summary>The owning user.</summary>
    public TKey UserId { get; set; } = default!;

    /// <summary>The rotation chain this token belongs to.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>SHA-256 hex digest of the secret half.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The user's security stamp at issuance; a rotated stamp voids the token.</summary>
    public string? SecurityStamp { get; set; }

    /// <summary>When the token was issued (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the token stops being redeemable (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When the token was redeemed; null while unused.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>When the token's family was revoked; null while live.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
