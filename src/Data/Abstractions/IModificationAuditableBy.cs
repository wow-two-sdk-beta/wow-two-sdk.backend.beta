namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with update user attribution.</summary>
/// <typeparam name="TUserId">The user-id type.</typeparam>
public interface IModificationAuditableBy<TUserId> : IEntity where TUserId : struct
{
    /// <summary>Gets or sets the user id that last updated the entity.</summary>
    TUserId UpdatedBy { get; set; }
}
