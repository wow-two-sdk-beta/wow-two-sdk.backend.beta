using System.Data;
using System.Reflection;
using Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Sessions;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Accesses straightforward entity tables through Dapper-generated CRUD SQL.</summary>
/// <remarks>
///   - column set defaults to every public instance property with both a getter and a setter
///   - override <see cref="ExcludedOnInsert"/> / <see cref="ExcludedOnUpdate"/> to omit generated columns
///   - id column comes from the <c>Id</c> property name
///   - concurrency tokens guard <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/>: an <see cref="IVersioned"/>
///     counter is checked and incremented, PostgreSQL <c>xmin</c> and SQL Server <see cref="IRowVersioned">rowversion</see>
///     are checked and read back; a stale token raises <see cref="ConcurrencyConflictException"/>
///   - when an <see cref="ITenantContext"/> is available, <see cref="IHasTenant{TTenantId}">IHasTenant&lt;string&gt;</see>
///     rows are stamped and restricted to the current tenant; no tenant means an explicit unscoped system operation
/// </remarks>
/// <typeparam name="TEntity">The entity type — must declare <see cref="IHasTableName"/>.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public class DapperRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IKeyedEntity<TId>, IHasTableName
    where TId : notnull, IEquatable<TId>
{
    private static readonly PropertyInfo[] ColumnProperties =
        typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0)
            .ToArray();

    private static readonly IReadOnlyList<string> AllProperties = ColumnProperties.Select(p => p.Name).ToArray();

    /// <summary>Whether any column is unsigned, which Npgsql and SqlClient reject as a parameter type.</summary>
    private static readonly bool HasUnsignedColumns = ColumnProperties.Any(p => IsUnsigned(p.PropertyType));

    private static readonly bool HasXmin = typeof(IHasXmin).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsVersioned = typeof(IVersioned).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsRowVersioned = typeof(IRowVersioned).IsAssignableFrom(typeof(TEntity));
    private static readonly bool HasConcurrencyToken = HasXmin || IsVersioned || IsRowVersioned;
    private static readonly bool IsSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity));
    private readonly IDataSession? _session;

    private const string IdProperty = nameof(IKeyedEntity<TId>.Id);
    private const string TenantIdProperty = nameof(IHasTenant<string>.TenantId);
    private static readonly bool IsTenantEntity = typeof(IHasTenant<string>).IsAssignableFrom(typeof(TEntity));

    /// <summary>The connection factory used for every operation.</summary>
    protected IDbConnectionFactory ConnectionFactory { get; }

    /// <summary>Gets the casing applied to every generated identifier.</summary>
    protected SqlNamingOptions Naming { get; }

    /// <summary>Gets the optional ambient tenant scope used by generated CRUD SQL.</summary>
    protected ITenantContext? TenantContext { get; }

    /// <summary>Initializes the repository over <paramref name="connectionFactory"/> with the default casing.</summary>
    /// <param name="connectionFactory">The connection factory used for every operation.</param>
    public DapperRepository(IDbConnectionFactory connectionFactory)
        : this(connectionFactory, new SqlNamingOptions())
    {
    }

    /// <summary>Initializes the repository over <paramref name="connectionFactory"/> with an explicit casing.</summary>
    /// <param name="connectionFactory">The connection factory used for every operation.</param>
    /// <param name="naming">The casing applied to every generated identifier.</param>
    public DapperRepository(IDbConnectionFactory connectionFactory, SqlNamingOptions naming)
        : this(connectionFactory, naming, null)
    {
    }

    /// <summary>Initializes an autonomous repository with naming and an ambient tenant scope.</summary>
    /// <param name="connectionFactory">The connection factory used for every operation.</param>
    /// <param name="naming">The casing applied to every generated identifier.</param>
    /// <param name="tenantContext">The ambient tenant scope for tenant-owned rows.</param>
    public DapperRepository(IDbConnectionFactory connectionFactory, SqlNamingOptions naming, ITenantContext? tenantContext)
        : this(connectionFactory, naming, tenantContext, null)
    {
    }

    /// <summary>Initializes the repository with explicit naming and an ambient tenant scope.</summary>
    /// <param name="connectionFactory">The connection factory used for every operation.</param>
    /// <param name="naming">The casing applied to every generated identifier.</param>
    /// <param name="tenantContext">The ambient tenant scope; pass it for tenant-owned rows.</param>
    /// <param name="session">The optional transaction owner; registered repositories join its active unit.</param>
    public DapperRepository(
        IDbConnectionFactory connectionFactory,
        SqlNamingOptions naming,
        ITenantContext? tenantContext = null,
        IDataSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(naming);
        ConnectionFactory = connectionFactory;
        Naming = naming;
        TenantContext = tenantContext;
        _session = session;
    }

    /// <summary>Property names omitted from <c>INSERT</c> column lists (identity / store-generated columns). Default: none.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnInsert => [];

    /// <summary>Property names omitted from <c>UPDATE</c> SET lists (identity / immutable / computed columns). Default: <c>Id</c>.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnUpdate => [IdProperty];

    /// <summary>Gets whether generated reads include logically deleted rows. Defaults to false.</summary>
    protected virtual bool IncludeSoftDeleted => false;

    private static string ReadColumns => HasXmin ? "*, xmin AS \"Xmin\"" : "*";
    private string SoftDeletePredicate => IsSoftDeletable && !IncludeSoftDeleted
        ? $" AND {SqlNamingMapper.Col(nameof(ISoftDeletable.IsDeleted), Naming.ColumnCase)} = FALSE"
        : string.Empty;

    private static string Table => TEntity.TableName;
    private string IdColumn => SqlNamingMapper.Col(IdProperty, Naming.ColumnCase);
    private string TenantIdColumn => SqlNamingMapper.Col(TenantIdProperty, Naming.ColumnCase);
    private string TenantIdParameter => SqlNamingMapper.Par(TenantIdProperty, Naming.ParameterCase);
    private string TenantIdParameterReference => SqlNamingMapper.ParRef(TenantIdProperty, Naming.ParameterCase);

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {ReadColumns} FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)}{SoftDeletePredicate}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" AND {TenantIdColumn} = {TenantIdParameterReference}";

        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Connection.QuerySingleOrDefaultAsync<TEntity>(
            new CommandDefinition(sql, ParamsForId(id, tenantId), transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {ReadColumns} FROM {Table} WHERE 1 = 1{SoftDeletePredicate}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" AND {TenantIdColumn} = {TenantIdParameterReference}";

        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await lease.Connection.QueryAsync<TEntity>(
            new CommandDefinition(sql, ParamsForTenant(tenantId), transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId;
        var tenantPredicate = tenantId is null ? string.Empty : $" AND {TenantIdColumn} = {TenantIdParameterReference}";
        var sql = $"SELECT EXISTS (SELECT 1 FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)}{tenantPredicate}{SoftDeletePredicate})";
        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, ParamsForId(id, tenantId), transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM {Table} WHERE 1 = 1{SoftDeletePredicate}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" AND {TenantIdColumn} = {TenantIdParameterReference}";

        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await lease.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, ParamsForTenant(tenantId), transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        StampCurrentTenant(entity);
        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await lease.Connection.ExecuteAsync(
            new CommandDefinition(InsertSql, WriteParameters(entity), transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return entity;
    }

    /// <inheritdoc />
    public virtual async Task CreateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var list = entities as ICollection<TEntity> ?? entities.ToList();
        if (list.Count == 0)
            return;

        foreach (var entity in list)
            StampCurrentTenant(entity);

        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Dapper executes the command once per element when passed an enumerable.
        await lease.Connection.ExecuteAsync(
            new CommandDefinition(InsertSql, list.Select(WriteParameters).ToList(), transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            ((IHasTenant<string>)entity).TenantId = tenantId;

        var tenantPredicate = tenantId is null ? string.Empty : $" AND {TenantIdColumn} = @{TenantIdProperty}";
        var sql = $"UPDATE {Table} SET {UpdateAssignments}{RowVersionOutput} WHERE {IdColumn} = @{IdProperty}{TokenPredicates}{tenantPredicate}{XminReturning}";
        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, TokenParameters(entity), transaction: lease.Transaction, cancellationToken: cancellationToken);
        if (!HasConcurrencyToken)
        {
            await lease.Connection.ExecuteAsync(command).ConfigureAwait(false);
            return;
        }

        if (HasXmin)
        {
            var xmin = await lease.Connection.ExecuteScalarAsync<long?>(command).ConfigureAwait(false) ?? throw Conflict(entity);
            ((IHasXmin)entity).Xmin = (uint)xmin;
        }
        else if (IsRowVersioned)
        {
            ((IRowVersioned)entity).RowVersion = await lease.Connection.ExecuteScalarAsync<byte[]?>(command).ConfigureAwait(false) ?? throw Conflict(entity);
        }
        else if (await lease.Connection.ExecuteAsync(command).ConfigureAwait(false) == 0)
        {
            throw Conflict(entity);
        }

        if (IsVersioned)
            ((IVersioned)entity).Version++;
    }

    /// <inheritdoc />
    /// <remarks>An entity with a concurrency token is deleted only while the token still matches.</remarks>
    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!HasConcurrencyToken)
        {
            await DeleteByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        var tenantId = CurrentTenantId;
        var parameters = ColumnParameters(entity);
        if (HasXmin)
            parameters.Add("XminToken", (long)((IHasXmin)entity).Xmin);
        var tenantPredicate = string.Empty;
        if (tenantId is not null)
        {
            parameters.Add("ScopeTenantId", tenantId);
            tenantPredicate = $" AND {TenantIdColumn} = @ScopeTenantId";
        }

        var sql = $"DELETE FROM {Table} WHERE {IdColumn} = @{IdProperty}{TokenPredicates}{tenantPredicate}";
        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await lease.Connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (affected == 0)
            throw Conflict(entity);
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var sql = $"DELETE FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" AND {TenantIdColumn} = {TenantIdParameterReference}";

        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await lease.Connection.ExecuteAsync(
            new CommandDefinition(sql, ParamsForId(id, tenantId), transaction: lease.Transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    private async ValueTask<DataConnectionLease> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return await _session.OpenConnectionAsync(ConnectionFactory, cancellationToken).ConfigureAwait(false);
        }
        return new DataConnectionLease(
            await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false),
            null,
            null);
    }

    private string? CurrentTenantId => IsTenantEntity ? TenantContext?.TenantId : null;

    private DynamicParameters ParamsForId(TId id, string? tenantId)
    {
        var parameters = new DynamicParameters();
        parameters.Add(SqlNamingMapper.Par(IdProperty, Naming.ParameterCase), id);
        AddTenantParameter(parameters, tenantId);
        return parameters;
    }

    private DynamicParameters? ParamsForTenant(string? tenantId)
    {
        if (tenantId is null)
            return null;

        var parameters = new DynamicParameters();
        AddTenantParameter(parameters, tenantId);
        return parameters;
    }

    private void AddTenantParameter(DynamicParameters parameters, string? tenantId)
    {
        if (tenantId is not null)
            parameters.Add(TenantIdParameter, tenantId);
    }

    private void StampCurrentTenant(TEntity entity)
    {
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            ((IHasTenant<string>)entity).TenantId = tenantId;
    }

    private string InsertSql
    {
        get
        {
            var columns = AllProperties.Where(p => !ExcludedOnInsert.Contains(p) && !IsStoreGeneratedToken(p)).ToArray();
            var columnList = string.Join(", ", columns.Select(column => SqlNamingMapper.Col(column, Naming.ColumnCase)));
            var valueList = string.Join(", ", columns.Select(p => "@" + p)); // Dapper binds @PropertyName from the entity
            return $"INSERT INTO {Table} ({columnList}) VALUES ({valueList})";
        }
    }

    /// <summary>The <c>SET</c> list: every updatable column, with an <see cref="IVersioned"/> counter incremented in place.</summary>
    private string UpdateAssignments
    {
        get
        {
            var columns = AllProperties.Where(p => !ExcludedOnUpdate.Contains(p) && p != IdProperty && !IsStoreGeneratedToken(p) && !(IsVersioned && p == nameof(IVersioned.Version)));
            var assignments = columns.Select(p => $"{SqlNamingMapper.Col(p, Naming.ColumnCase)} = @{p}");
            if (IsVersioned)
                assignments = assignments.Append($"{VersionColumn} = {VersionColumn} + 1");
            return string.Join(", ", assignments);
        }
    }

    /// <summary>The <c>WHERE</c> conditions each token adds: the row still carries the token the entity was read with.</summary>
    private string TokenPredicates
        => (IsVersioned ? $" AND {VersionColumn} = @{nameof(IVersioned.Version)}" : string.Empty)
            + (HasXmin ? " AND xmin::text::bigint = @XminToken" : string.Empty)
            + (IsRowVersioned ? $" AND {SqlNamingMapper.Col(nameof(IRowVersioned.RowVersion), Naming.ColumnCase)} = @{nameof(IRowVersioned.RowVersion)}" : string.Empty);

    /// <summary>PostgreSQL reads the new <c>xmin</c> back; a stale token returns no row.</summary>
    private static string XminReturning => HasXmin ? " RETURNING xmin::text::bigint" : string.Empty;

    /// <summary>SQL Server reads the new rowversion back; a stale token outputs no row.</summary>
    private string RowVersionOutput => IsRowVersioned && !HasXmin
        ? $" OUTPUT INSERTED.{SqlNamingMapper.Col(nameof(IRowVersioned.RowVersion), Naming.ColumnCase)}"
        : string.Empty;

    private string VersionColumn => SqlNamingMapper.Col(nameof(IVersioned.Version), Naming.ColumnCase);

    /// <summary>The entity's values, plus the <c>xmin</c> token as a number PostgreSQL compares without a cast.</summary>
    private static object TokenParameters(TEntity entity)
    {
        if (!HasXmin && !HasUnsignedColumns)
            return entity;

        var parameters = ColumnParameters(entity);
        if (HasXmin)
            parameters.Add("XminToken", (long)((IHasXmin)entity).Xmin);
        return parameters;
    }

    /// <summary>The entity itself, or its values with unsigned numbers widened, which every provider accepts.</summary>
    private static object WriteParameters(TEntity entity) => HasUnsignedColumns ? ColumnParameters(entity) : entity;

    private static DynamicParameters ColumnParameters(TEntity entity)
    {
        var parameters = new DynamicParameters();
        foreach (var property in ColumnProperties)
        {
            parameters.Add(property.Name, property.GetValue(entity) switch
            {
                uint value => (long)value,
                ushort value => (int)value,
                ulong value => (decimal)value,
                var value => value,
            });
        }

        return parameters;
    }

    private static bool IsUnsigned(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(uint) || type == typeof(ushort) || type == typeof(ulong);
    }

    /// <summary>Whether the store writes <paramref name="property"/> itself, so no statement may set it.</summary>
    private static bool IsStoreGeneratedToken(string property)
        => (HasXmin && property == nameof(IHasXmin.Xmin)) || (IsRowVersioned && property == nameof(IRowVersioned.RowVersion));

    private static ConcurrencyConflictException Conflict(TEntity entity) => new(typeof(TEntity), entity.Id);
}
