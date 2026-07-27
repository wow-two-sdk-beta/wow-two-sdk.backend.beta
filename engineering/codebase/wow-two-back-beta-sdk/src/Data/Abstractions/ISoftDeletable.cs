namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity that supports soft-delete.</summary>
public interface ISoftDeletable : IEntity
{
    /// <summary>Gets or sets a value indicating whether the entity is logically deleted.</summary>
    bool IsDeleted { get; set; }

    /// <summary>Gets or sets the timestamp when the entity was soft-deleted.</summary>
    DateTimeOffset? DeletedAt { get; set; }
}
