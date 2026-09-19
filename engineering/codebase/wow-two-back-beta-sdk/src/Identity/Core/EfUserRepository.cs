using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Accesses user entities through the application's EF Core context.</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
/// <typeparam name="TContext">The application DbContext hosting the identity schema.</typeparam>
public sealed class EfUserRepository<TUser, TKey, TContext> : IUserRepository<TUser, TKey>
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
    where TContext : DbContext
{
    private readonly TContext _context;

    /// <summary>Create the store over <paramref name="context"/>.</summary>
    /// <param name="context">The application DbContext.</param>
    public EfUserRepository(TContext context)
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

        var entry = _context.Entry(user);
        if (entry.State == EntityState.Detached)
        {
            var tracked = _context.ChangeTracker
                .Entries<TUser>()
                .FirstOrDefault(candidate =>
                    candidate.State != EntityState.Detached
                    && EqualityComparer<TKey>.Default.Equals(candidate.Entity.Id, user.Id));

            if (tracked is not null)
            {
                throw new InvalidOperationException(
                    $"Cannot update a detached '{typeof(TUser).Name}' while another instance with key '{user.Id}' is tracked. "
                    + "Apply accepted changes to the tracked instance.");
            }

            Users.Update(user);
        }

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
