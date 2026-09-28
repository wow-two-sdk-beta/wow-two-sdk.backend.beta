using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.UserClaims;

/// <summary>Defines per-user claim persistence.</summary>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public interface IUserClaimRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The user's claims.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Claim>> GetAsync(TKey userId, CancellationToken cancellationToken = default);

    /// <summary>Add claims to the user.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="claims">The claims.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(TKey userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default);

    /// <summary>Remove every claim of the same type and value; returns how many were removed.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="claim">The claim to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> RemoveAsync(TKey userId, Claim claim, CancellationToken cancellationToken = default);

    /// <summary>Replace every claim of the same type and value as <paramref name="claim"/>; returns how many were replaced.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="claim">The claim to replace.</param>
    /// <param name="replacement">The claim taking its place.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> ReplaceAsync(TKey userId, Claim claim, Claim replacement, CancellationToken cancellationToken = default);
}
