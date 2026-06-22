using System.Linq.Expressions;
using System.Reflection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Builds SQL identifiers (column names, parameter names, table references) from CLR property names using a single configurable casing convention, centralizing the snake_case-column / camelCase-param mapping that hand-written Dapper SQL otherwise repeats inline.</summary>
/// <remarks>Defaults: columns <see cref="CaseStyle.Snake"/>, parameters <see cref="CaseStyle.Camel"/>. Override the static defaults once at startup if your schema differs.</remarks>
public static class SqlNaming
{
    /// <summary>Casing applied to column names. Default <see cref="CaseStyle.Snake"/>.</summary>
    public static CaseStyle ColumnCase { get; set; } = CaseStyle.Snake;

    /// <summary>Casing applied to Dapper parameter names (without the <c>@</c>). Default <see cref="CaseStyle.Camel"/>.</summary>
    public static CaseStyle ParameterCase { get; set; } = CaseStyle.Camel;

    // ── Columns ──

    /// <summary>Column name for a property: <c>Col("OrderLineId")</c> → <c>order_line_id</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a column name.</param>
    public static string Col(string propertyName) => CaseConverter.ToCase(propertyName, ColumnCase);

    /// <summary>Aliased column reference: <c>Col("OrderLineId", "l")</c> → <c>l.order_line_id</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a column name.</param>
    /// <param name="alias">The table alias prefixed to the column.</param>
    public static string Col(string propertyName, string alias) => $"{alias}.{Col(propertyName)}";

    /// <summary>Column name from a property selector: <c>Col&lt;Order&gt;(o => o.LineId)</c> → <c>line_id</c>.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the column.</param>
    public static string Col<T>(Expression<Func<T, object?>> selector) => Col(PropertyName(selector));

    /// <summary>Aliased column from a property selector: <c>Col&lt;Order&gt;(o => o.LineId, "l")</c> → <c>l.line_id</c>.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the column.</param>
    /// <param name="alias">The table alias prefixed to the column.</param>
    public static string Col<T>(Expression<Func<T, object?>> selector, string alias) => Col(PropertyName(selector), alias);

    // ── Parameters ──

    /// <summary>Bare parameter name (no <c>@</c>): <c>Par("OrderLineId")</c> → <c>orderLineId</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a parameter name.</param>
    public static string Par(string propertyName) => CaseConverter.ToCase(propertyName, ParameterCase);

    /// <summary>Parameter placeholder with <c>@</c>: <c>ParRef("OrderLineId")</c> → <c>@orderLineId</c>.</summary>
    /// <param name="propertyName">The CLR property name to convert to a parameter placeholder.</param>
    public static string ParRef(string propertyName) => "@" + Par(propertyName);

    /// <summary>Bare parameter name from a property selector.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the parameter.</param>
    public static string Par<T>(Expression<Func<T, object?>> selector) => Par(PropertyName(selector));

    /// <summary>Parameter placeholder with <c>@</c> from a property selector.</summary>
    /// <typeparam name="T">The entity type the selector reads from.</typeparam>
    /// <param name="selector">The property-access expression identifying the parameter.</param>
    public static string ParRef<T>(Expression<Func<T, object?>> selector) => "@" + Par(PropertyName(selector));

    // ── Tables ──

    /// <summary>Table name for an entity declaring <see cref="IHasTableName"/>.</summary>
    public static string Table<TEntity>() where TEntity : IHasTableName => TEntity.TableName;

    /// <summary>Aliased table reference: <c>Table&lt;Order&gt;("o")</c> → <c>orders o</c>.</summary>
    /// <typeparam name="TEntity">The entity type declaring <see cref="IHasTableName"/>.</typeparam>
    /// <param name="alias">The table alias appended to the table name.</param>
    public static string Table<TEntity>(string alias) where TEntity : IHasTableName => $"{TEntity.TableName} {alias}";

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
