namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with a creation timestamp.</summary>
public interface ICreationAuditable : IEntity
{
    /// <summary>Gets or sets the timestamp when the entity was created.</summary>
    DateTimeOffset CreatedAt { get; set; }
}
