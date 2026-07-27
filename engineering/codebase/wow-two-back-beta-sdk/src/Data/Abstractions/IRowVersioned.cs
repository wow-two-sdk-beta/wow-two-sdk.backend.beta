namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Defines an entity with row version optimistic-concurrency tracking.</summary>
/// <remarks>For Postgres, prefer <see cref="IHasXmin"/> instead.</remarks>
public interface IRowVersioned : IEntity
{
    /// <summary>Gets or sets the binary concurrency token.</summary>
    byte[] RowVersion { get; set; }
}
