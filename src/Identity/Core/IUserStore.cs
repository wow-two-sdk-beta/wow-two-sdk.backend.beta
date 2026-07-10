namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>
/// The mandatory core store slice: user persistence + lookup by id / normalized name / normalized email. Optional
/// capabilities (password, email confirmation, lockout, …) are separate store slices layered on the same entity.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public interface IUserStore<TUser, in TKey>
    where TUser : class
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Persist a new user.</summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(TUser user, CancellationToken cancellationToken = default);

    /// <summary>Persist changes to an existing user.</summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(TUser user, CancellationToken cancellationToken = default);

    /// <summary>Delete a user.</summary>
    /// <param name="user">The user to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(TUser user, CancellationToken cancellationToken = default);

    /// <summary>Find a user by primary key, or null.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TUser?> FindByIdAsync(TKey userId, CancellationToken cancellationToken = default);

    /// <summary>Find a user by normalized user name, or null.</summary>
    /// <param name="normalizedUserName">The normalized user name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TUser?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);

    /// <summary>Find a user by normalized email, or null.</summary>
    /// <param name="normalizedEmail">The normalized email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
}
