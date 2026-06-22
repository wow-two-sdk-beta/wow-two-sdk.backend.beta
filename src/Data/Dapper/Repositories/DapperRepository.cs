using System.Data;
using System.Reflection;
using Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Dapper implementation of <see cref="IRepository{TEntity, TId}"/> for the hot read/CRUD path. SQL is generated from <see cref="IHasTableName"/> + <see cref="SqlNaming"/> + the entity's public read-write properties; intended for straightforward tables, complex queries are hand-written SQL.</summary>
/// <remarks>Column set defaults to every public instance property with a getter and setter, mapped to columns via <see cref="SqlNaming.ColumnCase"/>. Override <see cref="ExcludedOnInsert"/> / <see cref="ExcludedOnUpdate"/> to omit identity / computed / store-generated columns. The id column is taken from <c>nameof(IKeyedEntity&lt;TId&gt;.Id)</c>.</remarks>
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

    /// <summary>Initializes the repository over <paramref name="connectionFactory"/>.</summary>
    /// <param name="connectionFactory">The connection factory used for every operation.</param>
    public DapperRepository(IDbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ConnectionFactory = connectionFactory;
    }

    /// <summary>Property names omitted from <c>INSERT</c> column lists (identity / store-generated columns). Default: none.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnInsert => [];

    /// <summary>Property names omitted from <c>UPDATE</c> SET lists (identity / immutable / computed columns). Default: <c>Id</c>.</summary>
    protected virtual IReadOnlyCollection<string> ExcludedOnUpdate => [IdProperty];

    private static string Table => TEntity.TableName;
    private static string IdColumn => SqlNaming.Col(IdProperty);

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM {Table} WHERE {IdColumn} = {SqlNaming.ParRef(IdProperty)}";
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
        var sql = $"SELECT EXISTS (SELECT 1 FROM {Table} WHERE {IdColumn} = {SqlNaming.ParRef(IdProperty)})";
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
        var sql = $"DELETE FROM {Table} WHERE {IdColumn} = {SqlNaming.ParRef(IdProperty)}";
        await using var connection = await ConnectionFactory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, ParamsForId(id), cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    private static DynamicParameters ParamsForId(TId id)
    {
        var parameters = new DynamicParameters();
        parameters.Add(SqlNaming.Par(IdProperty), id);
        return parameters;
    }

    private string InsertSql
    {
        get
        {
            var columns = AllProperties.Where(p => !ExcludedOnInsert.Contains(p)).ToArray();
            var columnList = string.Join(", ", columns.Select(SqlNaming.Col));
            var valueList = string.Join(", ", columns.Select(p => "@" + p)); // Dapper binds @PropertyName from the entity
            return $"INSERT INTO {Table} ({columnList}) VALUES ({valueList})";
        }
    }

    private string UpdateSql
    {
        get
        {
            var columns = AllProperties.Where(p => !ExcludedOnUpdate.Contains(p) && p != IdProperty).ToArray();
            var assignments = string.Join(", ", columns.Select(p => $"{SqlNaming.Col(p)} = @{p}"));
            return $"UPDATE {Table} SET {assignments} WHERE {IdColumn} = @{IdProperty}";
        }
    }
}
