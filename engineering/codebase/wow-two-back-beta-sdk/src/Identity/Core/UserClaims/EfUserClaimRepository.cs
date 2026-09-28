using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.UserClaims;

/// <summary>Accesses per-user claims through the context hosting the identity schema.</summary>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
public sealed class EfUserClaimRepository<TKey>(DbContext context) : IUserClaimRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private DbSet<IdentityUserClaim<TKey>> Claims => context.Set<IdentityUserClaim<TKey>>();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Claim>> GetAsync(TKey userId, CancellationToken cancellationToken = default)
        => await Claims
            .Where(c => c.UserId.Equals(userId) && c.ClaimType != null)
            .OrderBy(c => c.Id)
            .Select(c => new Claim(c.ClaimType!, c.ClaimValue ?? string.Empty))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(TKey userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claims);
        Claims.AddRange(claims.Select(claim => new IdentityUserClaim<TKey> { UserId = userId, ClaimType = claim.Type, ClaimValue = claim.Value }));
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RemoveAsync(TKey userId, Claim claim, CancellationToken cancellationToken = default)
    {
        var matches = await MatchesAsync(userId, claim, cancellationToken);
        context.RemoveRange(matches);
        await context.SaveChangesAsync(cancellationToken);
        return matches.Count;
    }

    /// <inheritdoc />
    public async Task<int> ReplaceAsync(TKey userId, Claim claim, Claim replacement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var matches = await MatchesAsync(userId, claim, cancellationToken);
        foreach (var match in matches)
        {
            match.ClaimType = replacement.Type;
            match.ClaimValue = replacement.Value;
        }

        await context.SaveChangesAsync(cancellationToken);
        return matches.Count;
    }

    private Task<List<IdentityUserClaim<TKey>>> MatchesAsync(TKey userId, Claim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return Claims
            .Where(c => c.UserId.Equals(userId) && c.ClaimType == claim.Type && c.ClaimValue == claim.Value)
            .ToListAsync(cancellationToken);
    }
}
