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
            return IdentityResult.Failed(new IdentityError { Code = "UserNameRequired", Description = "A user name is required." });

        ApplyNormalization(user);
        user.SecurityStamp ??= NewStamp();
        user.ConcurrencyStamp ??= NewStamp();

        if (await repository.FindByNormalizedUserNameAsync(user.NormalizedUserName!, cancellationToken) is not null)
            return IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = $"User name '{user.UserName}' is already taken." });

        if (options.User.RequireUniqueEmail && !string.IsNullOrEmpty(user.NormalizedEmail)
            && await repository.FindByNormalizedEmailAsync(user.NormalizedEmail, cancellationToken) is not null)
            return IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = $"Email '{user.Email}' is already taken." });

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

    private void ApplyNormalization(TUser user)
    {
        user.NormalizedUserName = user.UserName.ToCanonical();
        user.NormalizedEmail = user.Email.ToCanonical();
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
