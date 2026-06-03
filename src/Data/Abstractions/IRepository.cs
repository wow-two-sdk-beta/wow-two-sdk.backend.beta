namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>
/// Read-write data access for an aggregate, keyed by <typeparamref name="TId"/>.
/// Verb convention is uniform across providers: <c>Create</c> brings a new entity into existence,
/// <c>Update</c> persists changes, <c>Delete</c> removes, <c>Get</c> reads (inherited).
/// </summary>
/// <remarks>
/// <c>Create</c> is deliberately distinct from "Add" — <c>Create</c> persists a brand-new entity,
/// whereas "Add" is reserved across the ecosystem for membership / collection operations
/// (e.g. add a user to a group). Implementations persist immediately (call SaveChanges); a
/// unit-of-work variant can layer on top later.
/// </remarks>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public interface IRepository<TEntity, TId> : IReadRepository<TEntity, TId>
    where TEntity : class, IKeyedEntity<TId>
    where TId : notnull, IEquatable<TId>
{
    /// <summary>Persists a new <paramref name="entity"/> and returns it (with any store-generated values populated).</summary>
    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Persists a batch of new entities.</summary>
    Task CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing <paramref name="entity"/>.</summary>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Removes an existing <paramref name="entity"/>.</summary>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Removes the entity with the given <paramref name="id"/> if it exists; returns whether a row was removed.</summary>
    Task<bool> DeleteByIdAsync(TId id, CancellationToken cancellationToken = default);
}
