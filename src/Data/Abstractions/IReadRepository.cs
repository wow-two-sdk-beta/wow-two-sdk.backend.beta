namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>
/// Read-only data access for an aggregate, keyed by <typeparamref name="TId"/>.
/// Verb convention is uniform across providers: <c>Get</c> for reads.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public interface IReadRepository<TEntity, in TId>
    where TEntity : class, IKeyedEntity<TId>
    where TId : notnull, IEquatable<TId>
{
    /// <summary>Gets the entity with the given <paramref name="id"/>, or <c>null</c> if none exists.</summary>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Gets every entity. Intended for small reference sets — prefer a filtered query for large tables.</summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Determines whether an entity with the given <paramref name="id"/> exists.</summary>
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Counts all entities.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
