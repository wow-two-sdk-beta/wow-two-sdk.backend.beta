using System.Linq.Expressions;
using System.Reflection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Maps a CLR property name to the SQL identifier that names it — a column, a parameter, or a table reference.</summary>
/// <remarks>
/// - every method takes its <see cref="CaseStyle"/> as an argument; the type holds no casing of its own
/// - a caller reading casing from configuration passes <see cref="SqlNamingOptions"/> members in
/// </remarks>
public static class SqlNamingMapper
{
    #region Columns

    /// <summary>Column name for a property: <c>Col("OrderLineId")</c> → <c>order_line_id</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a column name.</param>
    /// <param name="style">The casing emitted for the column name.</param>
    public static string Col(string propertyName, CaseStyle style) => CaseMapper.ToCase(propertyName, style);

    /// <summary>Aliased column reference: <c>Col("OrderLineId", "l")</c> → <c>l.order_line_id</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a column name.</param>
    /// <param name="alias">The table alias prefixed to the column.</param>
    /// <param name="style">The casing emitted for the column name.</param>
    public static string Col(string propertyName, string alias, CaseStyle style) => $"{alias}.{Col(propertyName, style)}";

    /// <summary>Column name from a property selector: <c>Col&lt;Order&gt;(o => o.LineId)</c> → <c>line_id</c>.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the column.</param>
    /// <param name="style">The casing emitted for the column name.</param>
    public static string Col<T>(Expression<Func<T, object?>> selector, CaseStyle style) => Col(PropertyName(selector), style);

    /// <summary>Aliased column from a property selector: <c>Col&lt;Order&gt;(o => o.LineId, "l")</c> → <c>l.line_id</c>.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the column.</param>
    /// <param name="alias">The table alias prefixed to the column.</param>
    /// <param name="style">The casing emitted for the column name.</param>
    public static string Col<T>(Expression<Func<T, object?>> selector, string alias, CaseStyle style) => Col(PropertyName(selector), alias, style);

    #endregion

    #region Parameters

    /// <summary>Bare parameter name (no <c>@</c>): <c>Par("OrderLineId")</c> → <c>orderLineId</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a parameter name.</param>
    /// <param name="style">The casing emitted for the parameter name.</param>
    public static string Par(string propertyName, CaseStyle style) => CaseMapper.ToCase(propertyName, style);

    /// <summary>Parameter placeholder with <c>@</c>: <c>ParRef("OrderLineId")</c> → <c>@orderLineId</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a parameter placeholder.</param>
    /// <param name="style">The casing emitted for the parameter name.</param>
    public static string ParRef(string propertyName, CaseStyle style) => "@" + Par(propertyName, style);

    /// <summary>Bare parameter name from a property selector.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the parameter.</param>
    /// <param name="style">The casing emitted for the parameter name.</param>
    public static string Par<T>(Expression<Func<T, object?>> selector, CaseStyle style) => Par(PropertyName(selector), style);

    /// <summary>Parameter placeholder with <c>@</c> from a property selector.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the parameter.</param>
    /// <param name="style">The casing emitted for the parameter name.</param>
    public static string ParRef<T>(Expression<Func<T, object?>> selector, CaseStyle style) => "@" + Par(PropertyName(selector), style);

    #endregion

    #region Tables

    /// <summary>Table name for an entity declaring <see cref="IHasTableName"/>.</summary>
    public static string Table<TEntity>() where TEntity : IHasTableName => TEntity.TableName;

    /// <summary>Aliased table reference: <c>Table&lt;Order&gt;("o")</c> → <c>orders o</c>.</summary>
    /// <typeparam name="TEntity">The entity type declaring <see cref="IHasTableName"/>.</typeparam>
    /// <param name="alias">The table alias appended to the table name.</param>
    public static string Table<TEntity>(string alias) where TEntity : IHasTableName => $"{TEntity.TableName} {alias}";

    #endregion

    /// <summary>Extracts the property name from a member-access selector, unwrapping the boxing convert that
    /// <c>Func&lt;T, object?&gt;</c> inserts for value-typed properties.</summary>
    internal static string PropertyName<T>(Expression<Func<T, object?>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : selector.Body;

        if (body is MemberExpression { Member: PropertyInfo property })
            return property.Name;

        throw new ArgumentException("Selector must be a property access expression (e.g. x => x.Name).", nameof(selector));
    }
}
