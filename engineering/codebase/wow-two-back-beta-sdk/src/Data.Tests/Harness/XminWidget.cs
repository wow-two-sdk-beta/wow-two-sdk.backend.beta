using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

public sealed class XminWidget : IKeyedEntity<Guid>, IHasTableName, IHasXmin
{
    public static string TableName => "xmin_widgets";
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint Xmin { get; set; }
}
