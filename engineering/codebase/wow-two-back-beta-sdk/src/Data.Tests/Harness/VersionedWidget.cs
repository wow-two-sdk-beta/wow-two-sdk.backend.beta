using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>A widget guarded by the portable <see cref="IVersioned"/> counter.</summary>
public sealed class VersionedWidget : IKeyedEntity<Guid>, IHasTableName, IVersioned
{
    public static string TableName => "versioned_widgets";

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public uint Version { get; set; }
}
