using System.Data.Common;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>
/// Opens a fresh <see cref="DbConnection"/> for ad-hoc Dapper queries. Implement once
/// per provider (Npgsql, Sqlite, SqlClient) — keeps the connection-string lookup in one place.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates a new closed connection. Caller is responsible for opening + disposing.</summary>
    DbConnection Create();

    /// <summary>Creates a new connection and opens it asynchronously.</summary>
    ValueTask<DbConnection> CreateOpenAsync(CancellationToken cancellationToken = default);
}
