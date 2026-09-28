using System.Linq.Expressions;
using System.Reflection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>
/// Builds the <see cref="EntitySpec"/> of <typeparamref name="TEntity"/> step by step, ending in <see cref="Build"/>.
/// Marker interfaces fill what the builder leaves unset: <see cref="IHasTableName"/> → table, <c>IKeyedEntity</c> →
/// key <c>Id</c>, <see cref="IVersioned"/> / <see cref="IHasXmin"/> / <see cref="IRowVersioned"/> → concurrency,
/// <see cref="ISoftDeletable"/> → soft delete, <c>IHasTenant</c> → tenant.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public sealed class EntitySpecBuilder<TEntity>
    where TEntity : class
{
    private readonly Dictionary<string, PropertySpecBuilder> _properties = new(StringComparer.Ordinal);
    private readonly List<IndexSpecBuilder> _indexes = [];
    private string? _table;
    private string? _schema;
    private string[]? _key;
    private ConcurrencySpec? _concurrency;
    private string? _softDelete;
    private string? _tenant;

    /// <summary>Maps the entity to <paramref name="table"/>.</summary>
    /// <param name="table">The table name.</param>
    /// <param name="schema">The schema; null takes the store's default.</param>
    public EntitySpecBuilder<TEntity> ToTable(string table, string? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        (_table, _schema) = (table, schema);
        return this;
    }

    /// <summary>Sets the key, composite when several properties are named.</summary>
    /// <param name="properties">The key properties, in order.</param>
    public EntitySpecBuilder<TEntity> HasKey(params Expression<Func<TEntity, object?>>[] properties)
    {
        ArgumentOutOfRangeException.ThrowIfZero(properties.Length);
        _key = [.. properties.Select(NameOf)];
        return this;
    }

    /// <summary>Configures one property.</summary>
    /// <param name="property">The property.</param>
    public PropertySpecBuilder Property(Expression<Func<TEntity, object?>> property)
    {
        var name = NameOf(property);
        if (!_properties.TryGetValue(name, out var builder))
            _properties[name] = builder = new PropertySpecBuilder(name);
        return builder;
    }

    /// <summary>Leaves a property without a column.</summary>
    /// <param name="property">The property.</param>
    public EntitySpecBuilder<TEntity> Ignore(Expression<Func<TEntity, object?>> property)
    {
        Property(property).Ignore();
        return this;
    }

    /// <summary>Adds an index over <paramref name="properties"/>.</summary>
    /// <param name="properties">The indexed properties, in order.</param>
    public IndexSpecBuilder HasIndex(params Expression<Func<TEntity, object?>>[] properties)
    {
        ArgumentOutOfRangeException.ThrowIfZero(properties.Length);
        var builder = new IndexSpecBuilder([.. properties.Select(NameOf)]);
        _indexes.Add(builder);
        return builder;
    }

    /// <summary>Guards the entity against lost updates with <paramref name="property"/>.</summary>
    /// <param name="property">The token property.</param>
    /// <param name="kind">How the token changes on each write.</param>
    public EntitySpecBuilder<TEntity> HasConcurrencyToken(Expression<Func<TEntity, object?>> property, ConcurrencyTokenKind kind)
    {
        _concurrency = new ConcurrencySpec { Property = NameOf(property), Kind = kind };
        return this;
    }

    /// <summary>Marks rows deleted through a boolean property instead of removing them.</summary>
    /// <param name="property">The boolean property.</param>
    public EntitySpecBuilder<TEntity> HasSoftDelete(Expression<Func<TEntity, bool>> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        _softDelete = MemberName(property.Body);
        return this;
    }

    /// <summary>Names the property holding the owning tenant.</summary>
    /// <param name="property">The tenant property.</param>
    public EntitySpecBuilder<TEntity> HasTenant(Expression<Func<TEntity, object?>> property)
    {
        _tenant = NameOf(property);
        return this;
    }

    /// <summary>Builds the spec, filling unset parts from the entity's marker interfaces.</summary>
    public EntitySpec Build()
    {
        var type = typeof(TEntity);
        return new EntitySpec
        {
            EntityType = type,
            Table = _table ?? type.GetProperty(nameof(IHasTableName.TableName), BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string,
            Schema = _schema,
            Key = _key ?? (Implements(type, typeof(IKeyedEntity<>)) ? ["Id"] : []),
            Properties = _properties.ToDictionary(pair => pair.Key, pair => pair.Value.Spec, StringComparer.Ordinal),
            Indexes = [.. _indexes.Select(index => index.Spec)],
            Concurrency = _concurrency ?? ConventionalToken(type),
            SoftDeleteProperty = _softDelete ?? (typeof(ISoftDeletable).IsAssignableFrom(type) ? nameof(ISoftDeletable.IsDeleted) : null),
            TenantProperty = _tenant ?? (Implements(type, typeof(IHasTenant<>)) ? "TenantId" : null),
        };
    }

    private static ConcurrencySpec? ConventionalToken(Type type)
    {
        if (typeof(IHasXmin).IsAssignableFrom(type))
            return new ConcurrencySpec { Property = nameof(IHasXmin.Xmin), Kind = ConcurrencyTokenKind.Xmin };
        if (typeof(IRowVersioned).IsAssignableFrom(type))
            return new ConcurrencySpec { Property = nameof(IRowVersioned.RowVersion), Kind = ConcurrencyTokenKind.RowVersion };
        return typeof(IVersioned).IsAssignableFrom(type)
            ? new ConcurrencySpec { Property = nameof(IVersioned.Version), Kind = ConcurrencyTokenKind.Counter }
            : null;
    }

    private static bool Implements(Type type, Type openInterface)
        => type.GetInterfaces().Any(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == openInterface);

    private static string NameOf(Expression<Func<TEntity, object?>> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return MemberName(property.Body);
    }

    private static string MemberName(Expression body) => body switch
    {
        MemberExpression member => member.Member.Name,
        UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Operand: MemberExpression member } => member.Member.Name,
        _ => throw new ArgumentException($"'{body}' must select a property of {typeof(TEntity).Name}.", nameof(body)),
    };
}
