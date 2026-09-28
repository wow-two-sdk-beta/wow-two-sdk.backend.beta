using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Logins;

/// <summary>Accesses external logins through the context hosting the identity schema.</summary>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <param name="context">The context hosting the identity schema.</param>
public sealed class EfUserLoginRepository<TKey>(DbContext context) : IUserLoginRepository<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private DbSet<IdentityUserLogin<TKey>> Logins => context.Set<IdentityUserLogin<TKey>>();

    /// <inheritdoc />
    public async Task AddAsync(IdentityUserLogin<TKey> login, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);
        Logins.Add(login);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(TKey userId, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
    {
        var login = await FindAsync(loginProvider, providerKey, cancellationToken);
        if (login is null || !login.UserId.Equals(userId))
            return false;

        Logins.Remove(login);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IdentityUserLogin<TKey>?> FindAsync(string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        => await Logins.FindAsync([loginProvider, providerKey], cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IdentityUserLogin<TKey>>> GetAsync(TKey userId, CancellationToken cancellationToken = default)
        => await Logins.Where(l => l.UserId.Equals(userId)).OrderBy(l => l.LoginProvider).ToListAsync(cancellationToken);
}
