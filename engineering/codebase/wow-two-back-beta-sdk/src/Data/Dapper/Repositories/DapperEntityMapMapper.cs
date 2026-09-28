using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using WoW.Two.Sdk.Backend.Beta.Data.Specs;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>
/// Maps an <see cref="EntitySpec"/> (registered, or read from the marker interfaces) and a naming convention to the SQL
/// shape the Dapper repository generates. DDL facets (lengths, precision, indexes) do not shape DML and are left out;
/// what the repository cannot honour — a key other than <c>Id</c>, a non-string tenant — follows <see cref="UnsupportedSpecMode"/>.
/// </summary>
internal static class DapperEntityMapMapper
{
    private const string MapperName = "Dapper";
    private static readonly ConcurrentDictionary<(Type Entity, EntitySpec? Spec, SqlNamingOptions Naming), DapperEntityMap> Cache = new(new KeyComparer());

    public static DapperEntityMap Map(Type entityType, EntitySpec conventional, EntitySpec? registered, SqlNamingOptions naming, UnsupportedSpecMode mode)
        => Cache.GetOrAdd((entityType, registered, naming), key => Build(key.Entity, registered ?? conventional, registered is not null, key.Naming, mode));

    private static DapperEntityMap Build(Type entityType, EntitySpec spec, bool registered, SqlNamingOptions naming, UnsupportedSpecMode mode)
    {
        var unsupported = new List<string>();
        var token = spec.Concurrency;
        var columns = new List<DapperColumnMap>();
        foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property is not { CanRead: true, CanWrite: true } || property.GetIndexParameters().Length > 0)
                continue;

            var facets = spec.Properties.GetValueOrDefault(property.Name);
            if (facets?.IsIgnored == true)
                continue;

            var storeGeneratedToken = token is { Kind: ConcurrencyTokenKind.Xmin or ConcurrencyTokenKind.RowVersion } && token.Property == property.Name;
            columns.Add(new DapperColumnMap
            {
                Property = property,
                Column = token is { Kind: ConcurrencyTokenKind.Xmin } && token.Property == property.Name
                    ? "xmin"
                    : facets?.Column ?? SqlNamingMapper.Col(property.Name, naming.ColumnCase),
                InsertExcluded = storeGeneratedToken || facets is { Generation: not ValueGenerationKind.None } || facets?.DefaultValueSql is not null || facets?.ComputedSql is not null,
                UpdateExcluded = storeGeneratedToken || facets is { Generation: ValueGenerationKind.OnAddOrUpdate } || facets?.ComputedSql is not null,
            });
        }

        var key = columns.FirstOrDefault(column => column.Property.Name == "Id")
            ?? throw new UnsupportedSpecException(MapperName, [$"{entityType.Name} has no Id property"]);
        if (spec.Key.Count > 0 && (spec.Key.Count != 1 || spec.Key[0] != "Id"))
            unsupported.Add($"{entityType.Name} key ({string.Join(", ", spec.Key)}): the repository keys on Id");

        (ConcurrencyTokenKind, DapperColumnMap)? tokenMap = null;
        if (token is not null)
        {
            var column = columns.FirstOrDefault(candidate => candidate.Property.Name == token.Property);
            if (column is null || !Fits(token.Kind, column.Property.PropertyType))
                unsupported.Add($"{entityType.Name}.{token.Property} ({token.Kind} concurrency on {column?.Property.PropertyType.Name ?? "a missing property"})");
            else
                tokenMap = (token.Kind, column);
        }

        var softDelete = Optional(spec.SoftDeleteProperty, typeof(bool), "soft delete", entityType, columns, unsupported, registered);
        var tenant = Optional(spec.TenantProperty, typeof(string), "tenant", entityType, columns, unsupported, registered);

        if (unsupported.Count > 0 && mode == UnsupportedSpecMode.Throw)
            throw new UnsupportedSpecException(MapperName, unsupported);

        return new DapperEntityMap
        {
            Table = spec.Schema is { Length: > 0 } schema && spec.Table is not null ? $"{schema}.{spec.Table}" : spec.Table ?? entityType.Name,
            Columns = columns,
            Key = key,
            Token = tokenMap,
            SoftDelete = softDelete,
            Tenant = tenant,
            ExplicitReads = registered,
        };
    }

    /// <summary>The column of an optional role, or null — reported as unsupported only when a registered spec declared it.</summary>
    private static DapperColumnMap? Optional(string? property, Type required, string role, Type entityType, List<DapperColumnMap> columns, List<string> unsupported, bool registered)
    {
        if (property is null)
            return null;

        var column = columns.FirstOrDefault(candidate => candidate.Property.Name == property);
        if (column is not null && (Nullable.GetUnderlyingType(column.Property.PropertyType) ?? column.Property.PropertyType) == required)
            return column;

        if (registered)
            unsupported.Add($"{entityType.Name}.{property} ({role} needs a {required.Name} property)");
        return null;
    }

    private static bool Fits(ConcurrencyTokenKind kind, Type type) => kind switch
    {
        ConcurrencyTokenKind.Counter => type == typeof(uint) || type == typeof(int) || type == typeof(long),
        ConcurrencyTokenKind.Xmin => type == typeof(uint),
        ConcurrencyTokenKind.RowVersion => type == typeof(byte[]),
        ConcurrencyTokenKind.Stamp => type == typeof(string),
        _ => false,
    };

    /// <summary>Compares specs by reference and naming by value, so one registry's spec maps once per casing.</summary>
    private sealed class KeyComparer : IEqualityComparer<(Type Entity, EntitySpec? Spec, SqlNamingOptions Naming)>
    {
        public bool Equals((Type Entity, EntitySpec? Spec, SqlNamingOptions Naming) x, (Type Entity, EntitySpec? Spec, SqlNamingOptions Naming) y)
            => x.Entity == y.Entity && ReferenceEquals(x.Spec, y.Spec) && x.Naming == y.Naming;

        public int GetHashCode((Type Entity, EntitySpec? Spec, SqlNamingOptions Naming) key)
            => HashCode.Combine(key.Entity, key.Spec is null ? 0 : RuntimeHelpers.GetHashCode(key.Spec), key.Naming);
    }
}
