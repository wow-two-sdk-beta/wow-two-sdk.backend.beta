using System.Reflection;
using WoW.Two.Sdk.Backend.Beta.Data.Specs;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Represents how one entity maps to generated SQL: table, columns, key, token, soft delete and tenant.</summary>
internal sealed class DapperTableModel
{
    /// <summary>The table, schema-qualified when the spec names a schema.</summary>
    public required string Table { get; init; }

    /// <summary>The mapped properties, ignored ones left out.</summary>
    public required IReadOnlyList<DapperColumnModel> Columns { get; init; }

    /// <summary>The key column, always the <c>Id</c> property.</summary>
    public required DapperColumnModel Key { get; init; }

    /// <summary>The concurrency token and its column; the column is <c>xmin</c> for <see cref="ConcurrencyTokenKind.Xmin"/>.</summary>
    public (ConcurrencyTokenKind Kind, DapperColumnModel Column)? Token { get; init; }

    /// <summary>The boolean column that hides deleted rows from reads.</summary>
    public DapperColumnModel? SoftDelete { get; init; }

    /// <summary>The string column that scopes rows to the current tenant.</summary>
    public DapperColumnModel? Tenant { get; init; }

    /// <summary>Whether reads list every column with an alias, as a registered spec may rename columns.</summary>
    public required bool ExplicitReads { get; init; }

    /// <summary>Whether a column is unsigned, which Npgsql and SqlClient reject as a parameter type.</summary>
    public bool HasUnsignedColumns => Columns.Any(column => IsUnsigned(column.Property.PropertyType));

    /// <summary>The column a property maps to, or null when it is ignored or unknown.</summary>
    public DapperColumnModel? Find(string property) => Columns.FirstOrDefault(column => column.Property.Name == property);

    private static bool IsUnsigned(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(uint) || type == typeof(ushort) || type == typeof(ulong);
    }
}
