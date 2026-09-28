using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Sessions;
using WoW.Two.Sdk.Backend.Beta.Data.Specs;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Accesses straightforward entity tables through Dapper-generated CRUD SQL.</summary>
/// <remarks>
///   - column set defaults to every public instance property with both a getter and a setter
///   - override <see cref="ExcludedOnInsert"/> / <see cref="ExcludedOnUpdate"/> to omit generated columns
///   - id column comes from the <c>Id</c> property name
///   - a registered <see cref="EntitySpec"/> renames the table and columns, ignores properties, leaves store-generated
///     columns out of writes, and declares the token, soft-delete and tenant properties; reads then alias every column
///   - concurrency tokens guard <see cref="UpdateAsync"/> and <see cref="DeleteAsync"/>: a counter is checked and
///     incremented, a stamp checked and replaced, PostgreSQL <c>xmin</c> and SQL Server rowversion checked and read back;
///     a stale token raises <see cref="ConcurrencyConflictException"/>
///   - when an <see cref="ITenantContext"/> is available, tenant-owned rows (a string tenant property) are stamped and
///     restricted to the current tenant; no tenant means an explicit unscoped system operation
/// </remarks>
/// <typeparam name="TEntity">The entity type — must declare <see cref="IHasTableName"/>.</typeparam>
/// <typeparam name="TId">The primary-key type.</typeparam>
public class DapperRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IKeyedEntity<TId>, IHasTableName
    where TId : notnull, IEquatable<TId>
{
    private const string IdProperty = nameof(IKeyedEntity<TId>.Id);

    /// <summary>The spec the marker interfaces imply, used when none is registered.</summary>
    private static readonly EntitySpec Conventional = new EntitySpecBuilder<TEntity>().Build();

    private readonly IDataSession? _session;
    private readonly DapperEntityMap _map;

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

    /// <summary>Initializes the repository with explicit naming, an ambient tenant scope and the registered entity specs.</summary>
    /// <param name="connectionFactory">The connection factory used for every operation.</param>
    /// <param name="naming">The casing applied to every generated identifier.</param>
    /// <param name="tenantContext">The ambient tenant scope; pass it for tenant-owned rows.</param>
    /// <param name="session">The optional transaction owner; registered repositories join its active unit.</param>
    /// <param name="specs">The entity specs; the one for <typeparamref name="TEntity"/> shapes the SQL when registered.</param>
    /// <param name="specOptions">What to do with spec features the repository cannot honour; the default throws.</param>
    /// <exception cref="UnsupportedSpecException">The spec asks for what the repository cannot do and the mode is Throw.</exception>
    public DapperRepository(
        IDbConnectionFactory connectionFactory,
        SqlNamingOptions naming,
        ITenantContext? tenantContext = null,
        IDataSession? session = null,
        EntitySpecRegistry? specs = null,
        IOptions<EntitySpecOptions>? specOptions = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(naming);
        ConnectionFactory = connectionFactory;
        Naming = naming;
        TenantContext = tenantContext;
        _session = session;
        _map = DapperEntityMapMapper.Map(
            typeof(TEntity),
            Conventional,
            specs?.Find(typeof(TEntity)),
            naming,
            specOptions?.Value.Unsupported ?? UnsupportedSpecMode.Throw);
    }

    /// <summary>Property names omitted from <c>INSERT</c> column lists (identity / store-generated columns). Default: none.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnInsert => [];

    /// <summary>Property names omitted from <c>UPDATE</c> SET lists (identity / immutable / computed columns). Default: <c>Id</c>.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnUpdate => [IdProperty];

    /// <summary>Gets whether generated reads include logically deleted rows. Defaults to false.</summary>
    protected virtual bool IncludeSoftDeleted => false;

    /// <summary>
    /// The select list: every mapped column aliased to its property under a registered spec, else <c>*</c>, which the
    /// snake_case convention maps; PostgreSQL's <c>xmin</c> is selected explicitly either way.
    /// </summary>
    private string ReadColumns => _map.ExplicitReads
        ? string.Join(", ", _map.Columns.Select(column => $"{column.Column} AS \"{column.Property.Name}\""))
        : _map.Token is { Kind: ConcurrencyTokenKind.Xmin } xmin ? $"*, xmin AS \"{xmin.Column.Property.Name}\"" : "*";

    private string SoftDeletePredicate => _map.SoftDelete is { } softDelete && !IncludeSoftDeleted
        ? $" AND {softDelete.Column} = FALSE"
        : string.Empty;

    private string Table => _map.Table;
    private string IdColumn => _map.Key.Column;
    private string TenantIdColumn => _map.Tenant!.Column;
    private string TenantIdParameter => SqlNamingMapper.Par(_map.Tenant!.Property.Name, Naming.ParameterCase);
    private string TenantIdParameterReference => SqlNamingMapper.ParRef(_map.Tenant!.Property.Name, Naming.ParameterCase);

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
        PrepareInsert(entity);
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
            PrepareInsert(entity);

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
            _map.Tenant!.Property.SetValue(entity, tenantId);

        var tenantPredicate = tenantId is null ? string.Empty : $" AND {TenantIdColumn} = @{_map.Tenant!.Property.Name}";
        var sql = $"UPDATE {Table} SET {UpdateAssignments}{RowVersionOutput} WHERE {IdColumn} = @{IdProperty}{TokenPredicates}{tenantPredicate}{XminReturning}";
        var stamp = _map.Token is { Kind: ConcurrencyTokenKind.Stamp } ? Guid.NewGuid().ToString("N") : null;
        await using var lease = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, TokenParameters(entity, stamp), transaction: lease.Transaction, cancellationToken: cancellationToken);
        if (_map.Token is not { } token)
        {
            await lease.Connection.ExecuteAsync(command).ConfigureAwait(false);
            return;
        }

        switch (token.Kind)
        {
            case ConcurrencyTokenKind.Xmin:
                var xmin = await lease.Connection.ExecuteScalarAsync<long?>(command).ConfigureAwait(false) ?? throw Conflict(entity);
                token.Column.Property.SetValue(entity, (uint)xmin);
                break;
            case ConcurrencyTokenKind.RowVersion:
                token.Column.Property.SetValue(entity, await lease.Connection.ExecuteScalarAsync<byte[]?>(command).ConfigureAwait(false) ?? throw Conflict(entity));
                break;
            default:
                if (await lease.Connection.ExecuteAsync(command).ConfigureAwait(false) == 0)
                    throw Conflict(entity);
                token.Column.Property.SetValue(entity, token.Kind == ConcurrencyTokenKind.Stamp ? stamp : Increment(token.Column.Property.GetValue(entity), token.Column.Property.PropertyType));
                break;
        }
    }

    /// <inheritdoc />
    /// <remarks>An entity with a concurrency token is deleted only while the token still matches.</remarks>
    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (_map.Token is null)
        {
            await DeleteByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        var tenantId = CurrentTenantId;
        var parameters = ColumnParameters(entity);
        AddXminToken(parameters, entity);
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

    private string? CurrentTenantId => _map.Tenant is null ? null : TenantContext?.TenantId;

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

    /// <summary>Stamps the current tenant and, for a stamp token, a first stamp before an insert.</summary>
    private void PrepareInsert(TEntity entity)
    {
        var tenantId = CurrentTenantId;
        if (tenantId is not null)
            _map.Tenant!.Property.SetValue(entity, tenantId);

        if (_map.Token is { Kind: ConcurrencyTokenKind.Stamp } token && string.IsNullOrEmpty(token.Column.Property.GetValue(entity) as string))
            token.Column.Property.SetValue(entity, Guid.NewGuid().ToString("N"));
    }

    private string InsertSql
    {
        get
        {
            var columns = _map.Columns.Where(column => !column.InsertExcluded && !ExcludedOnInsert.Contains(column.Property.Name)).ToArray();
            var columnList = string.Join(", ", columns.Select(column => column.Column));
            var valueList = string.Join(", ", columns.Select(column => "@" + column.Property.Name)); // Dapper binds @PropertyName from the entity
            return $"INSERT INTO {Table} ({columnList}) VALUES ({valueList})";
        }
    }

    /// <summary>The <c>SET</c> list: every updatable column, with a counter incremented in place and a stamp replaced.</summary>
    private string UpdateAssignments
    {
        get
        {
            var token = _map.Token;
            var assignments = _map.Columns
                .Where(column => !column.UpdateExcluded
                    && !ExcludedOnUpdate.Contains(column.Property.Name)
                    && column.Property.Name != IdProperty
                    && column != token?.Column)
                .Select(column => $"{column.Column} = @{column.Property.Name}");
            return string.Join(", ", token switch
            {
                { Kind: ConcurrencyTokenKind.Counter } counter => assignments.Append($"{counter.Column.Column} = {counter.Column.Column} + 1"),
                { Kind: ConcurrencyTokenKind.Stamp } stamp => assignments.Append($"{stamp.Column.Column} = @NewStamp"),
                _ => assignments,
            });
        }
    }

    /// <summary>The <c>WHERE</c> condition the token adds: the row still carries the token the entity was read with.</summary>
    private string TokenPredicates => _map.Token switch
    {
        { Kind: ConcurrencyTokenKind.Xmin } => " AND xmin::text::bigint = @XminToken",
        { } token => $" AND {token.Column.Column} = @{token.Column.Property.Name}",
        null => string.Empty,
    };

    /// <summary>PostgreSQL reads the new <c>xmin</c> back; a stale token returns no row.</summary>
    private string XminReturning => _map.Token is { Kind: ConcurrencyTokenKind.Xmin } ? " RETURNING xmin::text::bigint" : string.Empty;

    /// <summary>SQL Server reads the new rowversion back; a stale token outputs no row.</summary>
    private string RowVersionOutput => _map.Token is { Kind: ConcurrencyTokenKind.RowVersion } token
        ? $" OUTPUT INSERTED.{token.Column.Column}"
        : string.Empty;

    /// <summary>The entity itself, or its values when a token or an unsigned column needs parameters of its own.</summary>
    private object TokenParameters(TEntity entity, string? newStamp)
    {
        if (newStamp is null && _map.Token is not { Kind: ConcurrencyTokenKind.Xmin } && !_map.HasUnsignedColumns)
            return entity;

        var parameters = ColumnParameters(entity);
        AddXminToken(parameters, entity);
        if (newStamp is not null)
            parameters.Add("NewStamp", newStamp);
        return parameters;
    }

    /// <summary>The entity itself, or its values with unsigned numbers widened, which every provider accepts.</summary>
    private object WriteParameters(TEntity entity) => _map.HasUnsignedColumns ? ColumnParameters(entity) : entity;

    private DynamicParameters ColumnParameters(TEntity entity)
    {
        var parameters = new DynamicParameters();
        foreach (var column in _map.Columns)
        {
            parameters.Add(column.Property.Name, column.Property.GetValue(entity) switch
            {
                uint value => (long)value,
                ushort value => (int)value,
                ulong value => (decimal)value,
                var value => value,
            });
        }

        return parameters;
    }

    private void AddXminToken(DynamicParameters parameters, TEntity entity)
    {
        if (_map.Token is { Kind: ConcurrencyTokenKind.Xmin } xmin)
            parameters.Add("XminToken", (long)(uint)xmin.Column.Property.GetValue(entity)!);
    }

    /// <summary>The counter plus one, in the counter's own type (uint, int or long).</summary>
    private static object Increment(object? counter, Type type)
        => Convert.ChangeType(Convert.ToInt64(counter, CultureInfo.InvariantCulture) + 1, type, CultureInfo.InvariantCulture);

    private static ConcurrencyConflictException Conflict(TEntity entity) => new(typeof(TEntity), entity.Id);
}
