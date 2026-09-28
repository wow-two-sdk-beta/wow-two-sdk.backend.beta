namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Logins;

/// <summary>
/// Provides external logins: link a provider account (Google, Microsoft, Telegram, …) to a user, find the user behind
/// one, and unlink it. Unlinking rotates the security stamp. The OAuth handshake itself stays with the OAuth packages.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="logins">The login repository.</param>
/// <param name="accounts">The core account service.</param>
public sealed class UserLoginService<TUser, TKey>(IUserLoginRepository<TKey> logins, UserAccountService<TUser, TKey> accounts)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Link a provider account to <paramref name="user"/>; fails when it already belongs to any account.</summary>
    /// <param name="user">The user.</param>
    /// <param name="loginProvider">The provider, as its authentication scheme names it.</param>
    /// <param name="providerKey">The user's stable key at the provider (its subject).</param>
    /// <param name="displayName">Optional provider label for account settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> AddLoginAsync(TUser user, string loginProvider, string providerKey, string? displayName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(loginProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        if (await logins.FindAsync(loginProvider, providerKey, cancellationToken) is not null)
            return IdentityUserExtensions.Failure(IdentityErrorCodeConstants.LoginAlreadyAssociated, $"This {loginProvider} account is already linked.");

        await logins.AddAsync(
            new IdentityUserLogin<TKey> { UserId = user.Id, LoginProvider = loginProvider, ProviderKey = providerKey, ProviderDisplayName = displayName },
            cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Unlink a provider account from <paramref name="user"/> and rotate the stamp.</summary>
    /// <param name="user">The user.</param>
    /// <param name="loginProvider">The provider.</param>
    /// <param name="providerKey">The user's key at the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return await logins.RemoveAsync(user.Id, loginProvider, providerKey, cancellationToken)
            ? await accounts.RotateSecurityStampAsync(user, cancellationToken)
            : IdentityResult.Success;
    }

    /// <summary>The user linked to a provider account, or null.</summary>
    /// <param name="loginProvider">The provider.</param>
    /// <param name="providerKey">The user's key at the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<TUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        => await logins.FindAsync(loginProvider, providerKey, cancellationToken) is { } login
            ? await accounts.FindByIdAsync(login.UserId, cancellationToken)
            : null;

    /// <summary>The user's linked provider accounts.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<IdentityUserLogin<TKey>>> GetLoginsAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return logins.GetAsync(user.Id, cancellationToken);
    }
}
