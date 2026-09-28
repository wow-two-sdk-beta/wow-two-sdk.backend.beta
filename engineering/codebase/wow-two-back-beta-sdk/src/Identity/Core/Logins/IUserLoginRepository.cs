namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Logins;

/// <summary>Defines external-login persistence: which provider account belongs to which user.</summary>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public interface IUserLoginRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Link a provider account to a user.</summary>
    /// <param name="login">The login row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(IdentityUserLogin<TKey> login, CancellationToken cancellationToken = default);

    /// <summary>Unlink a provider account; returns whether one was removed.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="loginProvider">The provider.</param>
    /// <param name="providerKey">The user's key at the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> RemoveAsync(TKey userId, string loginProvider, string providerKey, CancellationToken cancellationToken = default);

    /// <summary>The login row for a provider account, or null.</summary>
    /// <param name="loginProvider">The provider.</param>
    /// <param name="providerKey">The user's key at the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IdentityUserLogin<TKey>?> FindAsync(string loginProvider, string providerKey, CancellationToken cancellationToken = default);

    /// <summary>The user's linked provider accounts.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<IdentityUserLogin<TKey>>> GetAsync(TKey userId, CancellationToken cancellationToken = default);
}
