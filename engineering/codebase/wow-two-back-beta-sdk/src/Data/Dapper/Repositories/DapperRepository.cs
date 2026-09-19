using System.Data;
using System.Reflection;
using Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Accesses straightforward entity tables through Dapper-generated CRUD SQL.</summary>
/// <remarks>
///   - column set defaults to every public instance property with both a getter and a setter
///   - override <see cref="ExcludedOnInsert"/> / <see cref="ExcludedOnUpdate"/> to omit generated columns
///   - id column comes from the <c>Id</c> property name
///   - when an <see cref="ITenantContext"/> is available, <see cref="IHasTenant{TTenantId}">IHasTenant&lt;string&gt;</see>
///     rows are stamped and restricted to the current tenant; no tenant means an explicit unscoped system operation
/// </remarks>
/// <typeparam name="TEntity">The entity type — must declare <see cref="IHasTableName"/>.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public class DapperRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IKeyedEntity<TId>, IHasTableName
    where TId : notnull, IEquatable<TId>
{
    private static readonly IReadOnlyList<string> AllProperties =
        typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .ToArray();

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

    /// <summary>Initializes the repository with explicit naming and an ambient tenant scope.</summary>
    /// <param name="connectionFactory">The connection factory used for every operation.</param>
    /// <param name="naming">The casing applied to every generated identifier.</param>
    /// <param name="tenantContext">The ambient tenant scope; pass it for tenant-owned rows.</param>
    public DapperRepository(
        IDbConnectionFactory connectionFactory,
        SqlNamingOptions naming,
        ITenantContext? tenantContext)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(naming);
        ConnectionFactory = connectionFactory;
        Naming = naming;
        TenantContext = tenantContext;
    }

    /// <summary>Property names omitted from <c>INSERT</c> column lists (identity / store-generated columns). Default: none.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnInsert => [];

    /// <summary>Property names omitted from <c>UPDATE</c> SET lists (identity / immutable / computed columns). Default: <c>Id</c>.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnUpdate => [IdProperty];

    private static string Table => TEntity.TableName;
    private string IdColumn => SqlNamingMapper.Col(IdProperty, Naming.ColumnCase);
    private string TenantIdColumn => SqlNamingMapper.Col(TenantIdProperty, Naming.ColumnCase);
    private string TenantIdParameter => SqlNamingMapper.Par(TenantIdProperty, Naming.ParameterCase);
    private string TenantIdParameterReference => SqlNamingMapper.ParRef(TenantIdProperty, Naming.ParameterCase);

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" AND {TenantIdColumn} = {TenantIdParameterReference}";

        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<TEntity>(
            new CommandDefinition(sql, ParamsForId(id, tenantId), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM {Table}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" WHERE {TenantIdColumn} = {TenantIdParameterReference}";

        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TEntity>(
            new CommandDefinition(sql, ParamsForTenant(tenantId), cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId;
        var tenantPredicate = tenantId is null ? string.Empty : $" AND {TenantIdColumn} = {TenantIdParameterReference}";
        var sql = $"SELECT EXISTS (SELECT 1 FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)}{tenantPredicate})";
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, ParamsForId(id, tenantId), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM {Table}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" WHERE {TenantIdColumn} = {TenantIdParameterReference}";

        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, ParamsForTenant(tenantId), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        StampCurrentTenant(entity);
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);
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

        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        // Dapper executes the command once per element when passed an enumerable.
        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, list, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            ((IHasTenant<string>)entity).TenantId = tenantId;

        var sql = tenantId is null
            ? UpdateSql
            : $"{UpdateSql} AND {TenantIdColumn} = @{TenantIdProperty}";
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await DeleteByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var sql = $"DELETE FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)}";
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            sql += $" AND {TenantIdColumn} = {TenantIdParameterReference}";

        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, ParamsForId(id, tenantId), cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
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
            var columns = AllProperties.Where(p => !ExcludedOnInsert.Contains(p)).ToArray();
            var columnList = string.Join(", ", columns.Select(column => SqlNamingMapper.Col(column, Naming.ColumnCase)));
            var valueList = string.Join(", ", columns.Select(p => "@" + p)); // Dapper binds @PropertyName from the entity
            return $"INSERT INTO {Table} ({columnList}) VALUES ({valueList})";
        }
    }

    private string UpdateSql
    {
        get
        {
            var columns = AllProperties.Where(p => !ExcludedOnUpdate.Contains(p) && p != IdProperty).ToArray();
            var assignments = string.Join(", ", columns.Select(p => $"{SqlNamingMapper.Col(p, Naming.ColumnCase)} = @{p}"));
            return $"UPDATE {Table} SET {assignments} WHERE {IdColumn} = @{IdProperty}";
        }
    }
}
