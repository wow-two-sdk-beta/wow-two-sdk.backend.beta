using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;

/// <summary>Defines role membership persistence, keyed by user and role ids.</summary>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public interface IUserRoleRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Add a membership.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(TKey userId, TKey roleId, CancellationToken cancellationToken = default);

    /// <summary>Remove a membership.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(TKey userId, TKey roleId, CancellationToken cancellationToken = default);

    /// <summary>Whether the membership exists.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ContainsAsync(TKey userId, TKey roleId, CancellationToken cancellationToken = default);

    /// <summary>The names of the user's roles.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(TKey userId, CancellationToken cancellationToken = default);

    /// <summary>The claims attached to any of the user's roles.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Claim>> GetRoleClaimsAsync(TKey userId, CancellationToken cancellationToken = default);

    /// <summary>The ids of the role's members.</summary>
    /// <param name="roleId">The role id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<TKey>> GetUserIdsAsync(TKey roleId, CancellationToken cancellationToken = default);
}
