using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>EF Core implementation of <see cref="IUserStore{TUser,TKey}"/> — persists via the app's <typeparamref name="TContext"/> and writes eagerly (each call saves).</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <typeparam name="TContext">The application DbContext hosting the identity schema.</typeparam>
public sealed class EfUserStore<TUser, TKey, TContext> : IUserStore<TUser, TKey>
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
    where TContext : DbContext
{
    private readonly TContext _context;

    /// <summary>Create the store over <paramref name="context"/>.</summary>
    /// <param name="context">The application DbContext.</param>
    public EfUserStore(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    private DbSet<TUser> Users => _context.Set<TUser>();

    /// <inheritdoc />
    public async Task CreateAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TUser?> FindByIdAsync(TKey userId, CancellationToken cancellationToken = default)
        => await Users.FindAsync([userId], cancellationToken);

    /// <inheritdoc />
    public Task<TUser?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        => Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);

    /// <inheritdoc />
    public Task<TUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        => Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
}
