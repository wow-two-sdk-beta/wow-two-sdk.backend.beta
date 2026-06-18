using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Registration helper for the SQLite <see cref="IDbConnectionFactory"/> seam.</summary>
public static class SqliteConnectionFactoryServiceCollectionExtensions
{
    /// <summary>Registers a <see cref="SqliteConnectionFactory"/> over <paramref name="connectionString"/> as the <see cref="IDbConnectionFactory"/>.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The SQLite connection string (e.g. <c>Data Source=app.db</c>).</param>
    public static IServiceCollection AddSqliteConnectionFactory(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(connectionString));
        return services;
    }
}
