using System.Data.Common;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Creates a fresh <see cref="DbConnection"/> for ad-hoc Dapper queries — implement once per provider (Npgsql, Sqlite, SqlClient).</summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates a new closed connection. Caller is responsible for opening and disposing.</summary>
    DbConnection Create();

    /// <summary>Creates a new connection and opens it asynchronously.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask<DbConnection> CreateOpenAsync(CancellationToken cancellationToken = default);
}
