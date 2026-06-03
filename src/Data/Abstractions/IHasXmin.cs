namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with Postgres <c>xmin</c> optimistic-concurrency tracking.</summary>
/// <remarks>For SqlServer, prefer <see cref="IRowVersioned"/> instead.</remarks>
public interface IHasXmin : IEntity
{
    /// <summary>Gets or sets the <c>xmin</c> concurrency token.</summary>
    uint Xmin { get; set; }
}
