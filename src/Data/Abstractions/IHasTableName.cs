namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>
/// Declares the storage table name for an entity type. The name is the single source of truth
/// for hand-written SQL (Dapper <c>FROM</c>/<c>JOIN</c> clauses) and table-name resolution helpers.
/// </summary>
/// <remarks>
/// Implement on the entity itself as a static abstract member so the name is available without an
/// instance: <c>public static string TableName => "order_line_items";</c>.
/// Provide the name in the storage casing your schema uses (typically snake_case for Postgres).
/// </remarks>
public interface IHasTableName
{
    /// <summary>Gets the storage table name for the entity type.</summary>
    static abstract string TableName { get; }
}
