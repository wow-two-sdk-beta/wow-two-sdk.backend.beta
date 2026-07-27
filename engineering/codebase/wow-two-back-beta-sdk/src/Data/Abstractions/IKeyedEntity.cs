namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with a primary key of type <typeparamref name="TId"/>.</summary>
/// <typeparam name="TId">The primary-key type must be non-null and equatable.</typeparam>
public interface IKeyedEntity<out TId> : IEntity where TId : notnull, IEquatable<TId>
{
    /// <summary>Gets the primary key of the entity.</summary>
    TId Id { get; }
}
