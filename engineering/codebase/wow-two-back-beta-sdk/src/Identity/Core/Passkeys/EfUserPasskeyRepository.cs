using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;

/// <summary>Accesses passkeys through the context hosting the identity schema.</summary>
/// <typeparam name="TKey">User key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
public sealed class EfUserPasskeyRepository<TKey>(DbContext context) : IUserPasskeyRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private DbSet<IdentityPasskey<TKey>> Passkeys => context.Set<IdentityPasskey<TKey>>();

    /// <inheritdoc />
    public async Task AddAsync(IdentityPasskey<TKey> passkey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passkey);
        Passkeys.Add(passkey);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IdentityPasskey<TKey>?> FindAsync(byte[] credentialId, CancellationToken cancellationToken = default)
        => Passkeys.AsNoTracking().FirstOrDefaultAsync(p => p.CredentialId == credentialId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IdentityPasskey<TKey>>> ListAsync(TKey userId, CancellationToken cancellationToken = default)
        => await Passkeys.AsNoTracking().Where(p => p.UserId.Equals(userId)).OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> RecordUseAsync(Guid id, uint signCount, DateTimeOffset ceremonyIssuedAt, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
        => await Passkeys.Where(p => p.Id == id && (p.LastUsedAt == null || p.LastUsedAt < ceremonyIssuedAt))
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.SignCount, signCount).SetProperty(p => p.LastUsedAt, usedAt), cancellationToken) == 1;

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(TKey userId, Guid id, CancellationToken cancellationToken = default)
        => await Passkeys.Where(p => p.Id == id && p.UserId.Equals(userId)).ExecuteDeleteAsync(cancellationToken) == 1;
}
