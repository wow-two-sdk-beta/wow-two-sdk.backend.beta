using System.Data;
using Dapper;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Handles <see cref="List{T}"/> columns returned as arrays.</summary>
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
