using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;

/// <summary>Registration helpers for a shared Npgsql data source.</summary>
public static class PostgresServiceCollectionExtensions
{
    /// <summary>Registers a shared <see cref="NpgsqlDataSource"/> built from <see cref="DatabaseOptions"/>, consumable by EF Core and Dapper.</summary>
    /// <remarks>Register Npgsql enum mappings inside the optional configurator.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">An optional callback for further data-source configuration.</param>
    public static IServiceCollection AddNpgsqlDataSource(
        this IServiceCollection services,
        Action<NpgsqlDataSourceBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(serviceProvider =>
        {
            var connectionString = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString;
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            configure?.Invoke(builder);
            return builder.Build();
        });

        services.AddSingleton<DbDataSource>(serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>());

        return services;
    }
}
