using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>
/// A user account with ASP.NET-Identity-shaped columns, persisted through the Data layer (audit stamps auto-applied).
/// Derive an app-specific <c>AppUser : IdentityUser</c> to add columns. Optional slices (password, email, lockout, …)
/// read/write the matching properties; core owns identity + lookup keys only.
/// </summary>
/// <typeparam name="TKey">Primary-key type (default <see cref="Guid"/>).</typeparam>
public class IdentityUser<TKey> : IKeyedEntity<TKey>, IAuditable
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Primary key. Store-generated for <see cref="Guid"/> keys.</summary>
    public TKey Id { get; set; } = default!;

    /// <summary>Login name.</summary>
    public string? UserName { get; set; }

    /// <summary>Upper-invariant <see cref="UserName"/> for case-insensitive unique lookup.</summary>
    public string? NormalizedUserName { get; set; }

    /// <summary>Email address.</summary>
    public string? Email { get; set; }

    /// <summary>Upper-invariant <see cref="Email"/> for case-insensitive lookup.</summary>
    public string? NormalizedEmail { get; set; }

    /// <summary>Whether the email address has been confirmed.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Salted + hashed password; null until a password slice sets it.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Value that changes on every credential change — invalidates issued tokens/cookies (set by the security-stamp slice).</summary>
    public string? SecurityStamp { get; set; }

    /// <summary>Value that changes on every persist — optimistic-concurrency guard (ASP.NET-Identity compatible).</summary>
    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Whether the phone number has been confirmed.</summary>
    public bool PhoneNumberConfirmed { get; set; }

    /// <summary>Whether two-factor authentication is enabled.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>UTC time the lockout ends; null or past = not locked out.</summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>Whether lockout is enabled for this user.</summary>
    public bool LockoutEnabled { get; set; }

    /// <summary>Consecutive failed access attempts (drives lockout).</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>When the row was created (UTC) — stamped by the audit interceptor.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the row was last modified (UTC) — stamped by the audit interceptor.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A user account keyed by <see cref="Guid"/> — the default shape.</summary>
public class IdentityUser : IdentityUser<Guid>;
