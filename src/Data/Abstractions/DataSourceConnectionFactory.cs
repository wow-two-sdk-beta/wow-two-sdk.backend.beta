using System.Data.Common;

namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Creates connections from a registered <see cref="DbDataSource"/> — e.g. a shared NpgsqlDataSource.</summary>
/// <param name="dataSource">The data source connections are opened from.</param>
public sealed class DataSourceConnectionFactory(DbDataSource dataSource) : IDbConnectionFactory
{
    /// <inheritdoc />
    public DbConnection Create() => dataSource.CreateConnection();

    /// <inheritdoc />
    public async ValueTask<DbConnection> CreateOpenAsync(CancellationToken cancellationToken = default)
        => await dataSource.OpenConnectionAsync(cancellationToken);
}
