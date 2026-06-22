namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Declares the storage table name for an entity type, used by hand-written SQL and table-name resolution helpers.</summary>
/// <remarks>Implement as a static abstract member in the schema's storage casing: <c>public static string TableName => "order_line_items";</c>.</remarks>
public interface IHasTableName
{
    /// <summary>Gets the storage table name for the entity type.</summary>
    static abstract string TableName { get; }
}
