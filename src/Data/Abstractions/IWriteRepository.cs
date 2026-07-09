namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines write-only data access for an aggregate, keyed by <typeparamref name="TId"/> (<c>Create</c>/<c>Update</c>/<c>Delete</c>). The read half is <see cref="IReadRepository{TEntity, TId}"/>; in a CQRS split writes bind to EF Core and reads to Dapper.</summary>
/// <remarks>Implementations persist immediately (call SaveChanges).</remarks>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public interface IWriteRepository<TEntity, in TId>
    where TEntity : class, IKeyedEntity<TId>
    where TId : notnull, IEquatable<TId>
{
    /// <summary>Persists a new <paramref name="entity"/> and returns it (with any store-generated values populated).</summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Persists a batch of new entities.</summary>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity whose changes to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Removes an existing <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Removes the entity with the given <paramref name="id"/> if it exists; returns whether a row was removed.</summary>
    /// <param name="id">The primary key of the entity to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<bool> DeleteByIdAsync(TId id, CancellationToken cancellationToken = default);
}
