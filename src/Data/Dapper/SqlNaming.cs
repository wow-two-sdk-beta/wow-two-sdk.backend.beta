using System.Linq.Expressions;
using System.Reflection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>
/// Builds SQL identifiers (column names, parameter names, table references) from CLR property names,
/// using a single configurable casing convention. Centralizes the snake_case-column / camelCase-param
/// mapping that hand-written Dapper SQL otherwise repeats inline.
/// </summary>
/// <remarks>
/// Defaults: columns <see cref="CaseStyle.Snake"/>, parameters <see cref="CaseStyle.Camel"/>.
/// Override the static defaults once at startup if your schema differs.
/// </remarks>
public static class SqlNaming
{
    /// <summary>Casing applied to column names. Default <see cref="CaseStyle.Snake"/>.</summary>
    public static CaseStyle ColumnCase { get; set; } = CaseStyle.Snake;

    /// <summary>Casing applied to Dapper parameter names (without the <c>@</c>). Default <see cref="CaseStyle.Camel"/>.</summary>
    public static CaseStyle ParameterCase { get; set; } = CaseStyle.Camel;

    // ── Columns ──

    /// <summary>Column name for a property: <c>Col("OrderLineId")</c> → <c>order_line_id</c>.</summary>
    public static string Col(string propertyName) => CaseConverter.ToCase(propertyName, ColumnCase);

    /// <summary>Aliased column reference: <c>Col("OrderLineId", "l")</c> → <c>l.order_line_id</c>.</summary>
    public static string Col(string propertyName, string alias) => $"{alias}.{Col(propertyName)}";

    /// <summary>Column name from a property selector: <c>Col&lt;Order&gt;(o => o.LineId)</c> → <c>line_id</c>.</summary>
    public static string Col<T>(Expression<Func<T, object?>> selector) => Col(PropertyName(selector));

    /// <summary>Aliased column from a property selector: <c>Col&lt;Order&gt;(o => o.LineId, "l")</c> → <c>l.line_id</c>.</summary>
    public static string Col<T>(Expression<Func<T, object?>> selector, string alias) => Col(PropertyName(selector), alias);

    // ── Parameters ──

    /// <summary>Bare parameter name (no <c>@</c>): <c>Par("OrderLineId")</c> → <c>orderLineId</c>.</summary>
    public static string Par(string propertyName) => CaseConverter.ToCase(propertyName, ParameterCase);

    /// <summary>Parameter placeholder with <c>@</c>: <c>ParRef("OrderLineId")</c> → <c>@orderLineId</c>.</summary>
    public static string ParRef(string propertyName) => "@" + Par(propertyName);

    /// <summary>Bare parameter name from a property selector.</summary>
    public static string Par<T>(Expression<Func<T, object?>> selector) => Par(PropertyName(selector));

    /// <summary>Parameter placeholder with <c>@</c> from a property selector.</summary>
    public static string ParRef<T>(Expression<Func<T, object?>> selector) => "@" + Par(PropertyName(selector));

    // ── Tables ──

    /// <summary>Table name for an entity declaring <see cref="IHasTableName"/>.</summary>
    public static string Table<TEntity>() where TEntity : IHasTableName => TEntity.TableName;

    /// <summary>Aliased table reference: <c>Table&lt;Order&gt;("o")</c> → <c>orders o</c>.</summary>
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
