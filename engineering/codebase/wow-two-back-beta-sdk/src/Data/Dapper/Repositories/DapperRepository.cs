using System.Data;
using System.Reflection;
using Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Dapper implementation of <see cref="IRepository{TEntity, TId}"/> for the hot read/CRUD path. SQL is generated from <see cref="IHasTableName"/> + <see cref="SqlNamingMapper"/> + the entity's public read-write properties; intended for straightforward tables, complex queries are hand-written SQL.</summary>
/// <remarks>
///   - column set defaults to every public instance property with both a getter and a setter
///   - override <see cref="ExcludedOnInsert"/> / <see cref="ExcludedOnUpdate"/> to omit generated columns
///   - id column comes from the <c>Id</c> property name
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

    /// <summary>The connection factory used for every operation.</summary>
    protected IDbConnectionFactory ConnectionFactory { get; }

    /// <summary>Gets the casing applied to every generated identifier.</summary>
    protected SqlNamingOptions Naming { get; }

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
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(naming);
        ConnectionFactory = connectionFactory;
        Naming = naming;
    }

    /// <summary>Property names omitted from <c>INSERT</c> column lists (identity / store-generated columns). Default: none.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnInsert => [];

    /// <summary>Property names omitted from <c>UPDATE</c> SET lists (identity / immutable / computed columns). Default: <c>Id</c>.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnUpdate => [IdProperty];

    private static string Table => TEntity.TableName;
    private string IdColumn => SqlNamingMapper.Col(IdProperty, Naming.ColumnCase);

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)}";
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<TEntity>(
            new CommandDefinition(sql, ParamsForId(id), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM {Table}";
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TEntity>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT EXISTS (SELECT 1 FROM {Table} WHERE {IdColumn} = {SqlNamingMapper.ParRef(IdProperty, Naming.ParameterCase)})";
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, ParamsForId(id), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM {Table}";
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
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

        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        // Dapper executes the command once per element when passed an enumerable.
        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, list, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(UpdateSql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, ParamsForId(id), cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    private DynamicParameters ParamsForId(TId id)
    {
        var parameters = new DynamicParameters();
        parameters.Add(SqlNamingMapper.Par(IdProperty, Naming.ParameterCase), id);
        return parameters;
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
