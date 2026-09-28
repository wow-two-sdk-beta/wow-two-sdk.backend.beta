using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

/// <summary>Accesses refresh tokens through the context hosting the identity schema; consumption is one conditional update.</summary>
/// <typeparam name="TKey">User key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
public sealed class EfRefreshTokenRepository<TKey>(DbContext context) : IRefreshTokenRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private DbSet<IdentityRefreshToken<TKey>> Tokens => context.Set<IdentityRefreshToken<TKey>>();

    /// <inheritdoc />
    public async Task AddAsync(IdentityRefreshToken<TKey> token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        Tokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IdentityRefreshToken<TKey>?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => Tokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryConsumeAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default)
        => await Tokens
            .Where(t => t.Id == id && t.ConsumedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ConsumedAt, at), cancellationToken) == 1;

    /// <inheritdoc />
    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => Tokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, at), cancellationToken);

    /// <inheritdoc />
    public Task RevokeUserAsync(TKey userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => Tokens
            .Where(t => t.UserId.Equals(userId) && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, at), cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        => Tokens.Where(t => t.ExpiresAt <= before).ExecuteDeleteAsync(cancellationToken);
}
