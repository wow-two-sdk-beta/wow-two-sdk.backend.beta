namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with an update timestamp.</summary>
public interface IModificationAuditable : IEntity
{
    /// <summary>Gets or sets the timestamp when the entity was last updated.</summary>
    DateTimeOffset UpdatedAt { get; set; }
}
