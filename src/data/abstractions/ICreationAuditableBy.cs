namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with creation user attribution.</summary>
/// <typeparam name="TUserId">The user-id type.</typeparam>
public interface ICreationAuditableBy<TUserId> : IEntity where TUserId : struct
{
    /// <summary>Gets or sets the user id that created the entity.</summary>
    TUserId CreatedBy { get; set; }
}
