using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.UserClaims;

/// <summary>
/// Provides per-user claims: read, add, remove and replace. Removing or replacing a claim rotates the security stamp, so
/// principals still carrying the old claim end; an added claim appears from the next sign-in.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="repository">The claim repository.</param>
/// <param name="accounts">The core account service, for stamp rotation.</param>
public sealed class UserClaimService<TUser, TKey>(IUserClaimRepository<TKey> repository, UserAccountService<TUser, TKey> accounts)
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The user's claims.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return repository.GetAsync(user.Id, cancellationToken);
    }

    /// <summary>Add claims to the user.</summary>
    /// <param name="user">The user.</param>
    /// <param name="claims">The claims.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await repository.AddAsync(user.Id, claims, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>Remove a claim and rotate the security stamp when one was removed.</summary>
    /// <param name="user">The user.</param>
    /// <param name="claim">The claim.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> RemoveClaimAsync(TUser user, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return await repository.RemoveAsync(user.Id, claim, cancellationToken) > 0
            ? await accounts.RotateSecurityStampAsync(user, cancellationToken)
            : IdentityResult.Success;
    }

    /// <summary>Replace a claim and rotate the security stamp when one was replaced.</summary>
    /// <param name="user">The user.</param>
    /// <param name="claim">The claim to replace.</param>
    /// <param name="replacement">The claim taking its place.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdentityResult> ReplaceClaimAsync(TUser user, Claim claim, Claim replacement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return await repository.ReplaceAsync(user.Id, claim, replacement, cancellationToken) > 0
            ? await accounts.RotateSecurityStampAsync(user, cancellationToken)
            : IdentityResult.Success;
    }
}
