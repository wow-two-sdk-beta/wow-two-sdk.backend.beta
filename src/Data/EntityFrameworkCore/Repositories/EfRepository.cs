using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TEntity, TId}"/>.
/// Each write persists immediately via <c>SaveChangesAsync</c>. Reads honor any global query filters
/// configured on the context (e.g. the SDK soft-delete filter).
/// </summary>
/// <remarks>
/// The constructor takes the base <see cref="DbContext"/> so the type stays arity-2 and can be
/// registered as an open generic. In multi-context apps, subclass per context (or register the
/// target context as <see cref="DbContext"/>) — see <c>AddEfRepositories</c>.
/// </remarks>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public class EfRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IKeyedEntity<TId>
    where TId : notnull, IEquatable<TId>
{
    /// <summary>The underlying context.</summary>
    protected DbContext Context { get; }

    /// <summary>The entity set for <typeparamref name="TEntity"/>.</summary>
    protected DbSet<TEntity> Set { get; }

    /// <summary>Initializes the repository over <paramref name="context"/>.</summary>
    public EfRepository(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        Set = context.Set<TEntity>();
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
        => Set.AnyAsync(e => e.Id.Equals(id), cancellationToken);

    /// <inheritdoc />
    public virtual Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Set.CountAsync(cancellationToken);

    /// <inheritdoc />
    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await Set.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    /// <inheritdoc />
    public virtual async Task CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        await Set.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Update(entity);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Remove(entity);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return false;

        Set.Remove(entity);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
