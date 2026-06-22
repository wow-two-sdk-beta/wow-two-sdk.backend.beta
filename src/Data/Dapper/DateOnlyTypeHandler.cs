using System.Data;
using Dapper;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Bridges <see cref="DateOnly"/> CLR properties to providers that return <see cref="DateTime"/> for <c>DATE</c> columns (e.g. Npgsql).</summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    /// <inheritdoc />
    public override DateOnly Parse(object value)
        => value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            DateOnly d => d,
            string s => DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidCastException($"Cannot map {value.GetType()} to DateOnly.")
        };
}
