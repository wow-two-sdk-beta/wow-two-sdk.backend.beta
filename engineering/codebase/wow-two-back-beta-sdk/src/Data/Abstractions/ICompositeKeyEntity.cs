namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity keyed on two or more columns, with no single <c>Id</c>.</summary>
/// <remarks>
/// The shape a join or link row takes. The provider keys on the real columns, so the key is declared in the
/// entity configuration rather than exposed as a member.
/// </remarks>
public interface ICompositeKeyEntity : IEntity;
