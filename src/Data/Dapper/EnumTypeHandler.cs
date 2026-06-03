using System.Data;
using Dapper;
using WoW.Two.Sdk.Backend.Beta.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>
/// Maps an enum to and from a case-styled string column, reversibly, across any provider.
/// Use this when the database stores enums as text (the portable default). For Postgres native
/// enum types, prefer Npgsql's driver-level <c>MapEnum</c> instead.
/// </summary>
/// <remarks>
/// Register per enum: <c>SqlMapper.AddTypeHandler(new EnumTypeHandler&lt;OrderStatus&gt;());</c>.
/// Reads accept any casing of the label (case-insensitive); writes emit <see cref="Style"/>.
/// </remarks>
/// <typeparam name="TEnum">The enum type stored as text.</typeparam>
public sealed class EnumTypeHandler<TEnum> : SqlMapper.TypeHandler<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>The casing emitted on write. Defaults to <see cref="CaseStyle.Snake"/>.</summary>
    public CaseStyle Style { get; }

    /// <summary>Creates a handler emitting <paramref name="style"/> labels (default <see cref="CaseStyle.Snake"/>).</summary>
    public EnumTypeHandler(CaseStyle style = CaseStyle.Snake) => Style = style;

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, TEnum value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = EnumNameConverter<TEnum>.ToLabel(value, Style);
    }

    /// <inheritdoc />
    public override TEnum Parse(object value)
    {
        if (value is TEnum already)
            return already;

        var label = value as string
            ?? throw new InvalidCastException($"Cannot map {value?.GetType().Name ?? "null"} to enum '{typeof(TEnum).Name}'.");

        if (EnumNameConverter<TEnum>.TryParse(label, Style, out var parsed))
            return parsed;

        throw new InvalidCastException($"'{label}' does not map to any member of enum '{typeof(TEnum).Name}'.");
    }
}
