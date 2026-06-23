using System.Data.Common;
using Microsoft.Data.Sqlite;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Creates SQLite connections from a fixed connection string — the connection seam for SQLite hosts, which ship no <see cref="DbDataSource"/>.</summary>
/// <remarks>SQLite has no ADO.NET <see cref="DbDataSource"/>, so <c>AddDataSourceConnectionFactory</c> does not apply; register this via <c>AddSqliteConnectionFactory</c> instead.</remarks>
/// <param name="connectionString">The SQLite connection string used for every created connection.</param>
public sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
{
    /// <inheritdoc />
    public DbConnection Create() => new SqliteConnection(connectionString);

    /// <inheritdoc />
    public async ValueTask<DbConnection> CreateOpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
