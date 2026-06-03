namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with user attribution on soft-delete.</summary>
/// <typeparam name="TUserId">The user-id type.</typeparam>
public interface ISoftDeletableBy<TUserId> : IEntity where TUserId : struct
{
    /// <summary>Gets or sets the user id that soft-deleted the entity.</summary>
    TUserId DeletedBy { get; set; }
}
