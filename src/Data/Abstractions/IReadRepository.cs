namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines read-only data access for an aggregate, keyed by <typeparamref name="TId"/> (<c>Get</c> reads).</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public interface IReadRepository<TEntity, in TId>
    where TEntity : class, IKeyedEntity<TId>
    where TId : notnull, IEquatable<TId>
{
    /// <summary>Gets the entity with the given <paramref name="id"/>, or <c>null</c> if none exists.</summary>
    /// <param name="id">The primary key to look up.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Gets every entity. Intended for small reference sets — prefer a filtered query for large tables.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Determines whether an entity with the given <paramref name="id"/> exists.</summary>
    /// <param name="id">The primary key to probe for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Counts all entities.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
