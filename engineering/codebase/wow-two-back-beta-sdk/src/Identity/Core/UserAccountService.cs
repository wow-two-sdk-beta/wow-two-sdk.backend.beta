using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>
/// Provides the app-facing account operations over the identity slices. Core composes only <see cref="IUserRepository{TUser,TKey}"/>
/// (normalize keys, enforce uniqueness, stamp security/concurrency); optional slices extend it. Replaces ASP.NET Identity's
/// <c>UserManager</c> god-object: capabilities not registered are simply absent rather than silently no-op.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The core user store.</param>
/// <param name="options">Identity options.</param>
public sealed class UserAccountService<TUser, TKey>(IUserRepository<TUser, TKey> repository, IdentityCoreOptions options)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Create a user: normalize keys, stamp security/concurrency, enforce unique user name (and email when required), then persist.</summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(user.UserName))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.UserNameRequired, "A user name is required.");

        ApplyNormalization(user);
        user.SecurityStamp ??= NewStamp();
        user.ConcurrencyStamp ??= NewStamp();
        user.LockoutEnabled = options.Lockout.EnabledForNewUsers;

        if (await repository.FindByNormalizedUserNameAsync(user.NormalizedUserName!, cancellationToken) is not null)
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.DuplicateUserName, $"User name '{user.UserName}' is already taken.", IdentityUserExtensions.Param("UserName", user.UserName));

        if (await IsEmailTakenAsync(user.NormalizedEmail, cancellationToken: cancellationToken))
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.DuplicateEmail, $"Email '{user.Email}' is already taken.", IdentityUserExtensions.Param("Email", user.Email));

        await repository.CreateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Re-normalize keys and persist changes to an existing user.</summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ApplyNormalization(user);
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Rotate the security stamp and persist it, voiding every cookie, token and purpose token bound to the old stamp.</summary>
    /// <param name="user">The user to sign out everywhere.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> RotateSecurityStampAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.RotateSecurityStamp();
        await repository.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Whether another account holds <paramref name="normalizedEmail"/> while unique emails are required.</summary>
    /// <param name="normalizedEmail">The normalized email to test; null or empty is never taken.</param>
    /// <param name="owner">The account allowed to hold it already, when re-confirming its own address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> IsEmailTakenAsync(string? normalizedEmail, TUser? owner = null, CancellationToken cancellationToken = default)
    {
        if (!options.User.RequireUniqueEmail || string.IsNullOrEmpty(normalizedEmail))
            return false;

        var holder = await repository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        return holder is not null && (owner is null || !holder.Id.Equals(owner.Id));
    }

    /// <summary>Delete a user.</summary>
    /// <param name="user">The user to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await repository.DeleteAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Find a user by id, or null.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> FindByIdAsync(TKey userId, CancellationToken cancellationToken = default)
        => repository.FindByIdAsync(userId, cancellationToken);

    /// <summary>Find a user by user name (case-insensitive), or null.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> FindByNameAsync(string userName, CancellationToken cancellationToken = default)
        => repository.FindByNormalizedUserNameAsync(userName.ToCanonical() ?? string.Empty, cancellationToken);

    /// <summary>Find a user by email (case-insensitive), or null.</summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => repository.FindByNormalizedEmailAsync(email.ToCanonical() ?? string.Empty, cancellationToken);

    private static void ApplyNormalization(TUser user)
    {
        user.NormalizedUserName = user.UserName.ToCanonical();
        user.NormalizedEmail = user.Email.ToCanonical();
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
