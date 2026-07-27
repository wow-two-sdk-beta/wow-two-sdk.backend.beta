namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with numeric version optimistic-concurrency tracking.</summary>
/// <remarks>Provider-agnostic alternative to <see cref="IRowVersioned"/> and <see cref="IHasXmin"/>.</remarks>
public interface IVersioned : IEntity
{
    /// <summary>Gets or sets the numeric concurrency token.</summary>
    uint Version { get; set; }
}
