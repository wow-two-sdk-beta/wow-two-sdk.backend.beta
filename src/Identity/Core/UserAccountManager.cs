namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>
/// The thin facade over the identity slices — the app-facing surface. Core composes only <see cref="IUserStore{TUser,TKey}"/>
/// (normalize keys, enforce uniqueness, stamp security/concurrency); optional slices extend it. Replaces ASP.NET Identity's
/// <c>UserManager</c> god-object: capabilities not registered are simply absent rather than silently no-op.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public sealed class UserAccountManager<TUser, TKey>
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IUserStore<TUser, TKey> _store;
    private readonly ILookupNormalizer _normalizer;
    private readonly IdentityCoreOptions _options;

    /// <summary>Create the manager.</summary>
    /// <param name="store">The core user store.</param>
    /// <param name="normalizer">The lookup-key normalizer.</param>
    /// <param name="options">Identity options.</param>
    public UserAccountManager(IUserStore<TUser, TKey> store, ILookupNormalizer normalizer, IdentityCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(options);
        _store = store;
        _normalizer = normalizer;
        _options = options;
    }

    /// <summary>Create a user: normalize keys, stamp security/concurrency, enforce unique user name (and email when required), then persist.</summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(user.UserName))
            return IdentityResult.Failed(new IdentityError("UserNameRequired", "A user name is required."));

        ApplyNormalization(user);
        user.SecurityStamp ??= NewStamp();
        user.ConcurrencyStamp ??= NewStamp();

        if (await _store.FindByNormalizedUserNameAsync(user.NormalizedUserName!, cancellationToken) is not null)
            return IdentityResult.Failed(new IdentityError("DuplicateUserName", $"User name '{user.UserName}' is already taken."));

        if (_options.User.RequireUniqueEmail && !string.IsNullOrEmpty(user.NormalizedEmail)
            && await _store.FindByNormalizedEmailAsync(user.NormalizedEmail, cancellationToken) is not null)
            return IdentityResult.Failed(new IdentityError("DuplicateEmail", $"Email '{user.Email}' is already taken."));

        await _store.CreateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Re-normalize keys and persist changes to an existing user.</summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ApplyNormalization(user);
        await _store.UpdateAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Delete a user.</summary>
    /// <param name="user">The user to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _store.DeleteAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Find a user by id, or null.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> FindByIdAsync(TKey userId, CancellationToken cancellationToken = default)
        => _store.FindByIdAsync(userId, cancellationToken);

    /// <summary>Find a user by user name (case-insensitive), or null.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> FindByNameAsync(string userName, CancellationToken cancellationToken = default)
        => _store.FindByNormalizedUserNameAsync(_normalizer.Normalize(userName) ?? string.Empty, cancellationToken);

    /// <summary>Find a user by email (case-insensitive), or null.</summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _store.FindByNormalizedEmailAsync(_normalizer.Normalize(email) ?? string.Empty, cancellationToken);

    private void ApplyNormalization(TUser user)
    {
        user.NormalizedUserName = _normalizer.Normalize(user.UserName);
        user.NormalizedEmail = _normalizer.Normalize(user.Email);
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
