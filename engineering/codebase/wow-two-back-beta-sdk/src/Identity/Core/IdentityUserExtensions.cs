namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Applies the stamp and lockout column rules shared by the identity slices.</summary>
internal static class IdentityUserExtensions
{
    /// <summary>Replaces the security stamp, voiding tokens, cookies and purpose tokens bound to the previous one.</summary>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="user">The user whose stamp rotates.</param>
    internal static void RotateSecurityStamp<TKey>(this IdentityUser<TKey> user)
        where TKey : notnull, IEquatable<TKey>
        => user.SecurityStamp = Guid.NewGuid().ToString("N");

    /// <summary>Whether the user is inside an active lockout window at <paramref name="now"/>.</summary>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="user">The user to inspect.</param>
    /// <param name="now">The current instant.</param>
    internal static bool IsLockedOutAt<TKey>(this IdentityUser<TKey> user, DateTimeOffset now)
        where TKey : notnull, IEquatable<TKey>
        => user.LockoutEnabled && user.LockoutEnd is { } end && end > now;

    /// <summary>Creates a failed result carrying one error.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="description">The human-readable description.</param>
    internal static IdentityResult Failure(string code, string description)
        => IdentityResult.Failed(new IdentityError { Code = code, Description = description });
}
