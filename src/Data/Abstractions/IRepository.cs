namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines read-write data access for an aggregate, keyed by <typeparamref name="TId"/> — the union of <see cref="IReadRepository{TEntity, TId}"/> (<c>Get</c>) and <see cref="IWriteRepository{TEntity, TId}"/> (<c>Create</c>/<c>Update</c>/<c>Delete</c>).</summary>
/// <remarks>Convenience union for single-backend use. For a CQRS split, depend on <see cref="IReadRepository{TEntity, TId}"/> (Dapper) and <see cref="IWriteRepository{TEntity, TId}"/> (EF Core) directly instead.</remarks>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public interface IRepository<TEntity, TId> : IReadRepository<TEntity, TId>, IWriteRepository<TEntity, TId>
    where TEntity : class, IKeyedEntity<TId>
    where TId : notnull, IEquatable<TId>;
