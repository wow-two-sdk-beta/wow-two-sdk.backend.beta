using System.Data;
using Dapper;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Bridges <see cref="List{T}"/> CLR properties to providers that return arrays for array-typed columns (e.g. Postgres <c>TEXT[]</c>, <c>INT[]</c>).</summary>
public sealed class ListTypeHandler<T> : SqlMapper.TypeHandler<List<T>>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, List<T>? value)
    {
        parameter.Value = value?.ToArray() ?? (object)DBNull.Value;
    }

    /// <inheritdoc />
    public override List<T> Parse(object value)
        => value switch
        {
            null or DBNull => [],
            T[] arr => [.. arr],
            IEnumerable<T> seq => [.. seq],
            _ => throw new InvalidCastException($"Cannot map {value.GetType()} to List<{typeof(T).Name}>.")
        };
}
