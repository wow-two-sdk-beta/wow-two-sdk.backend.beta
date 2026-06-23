using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Data;

/// <summary>One-call Postgres persistence registration composing the SDK's data-source, connection-factory, audit-interceptor, EF Core, and bespoke-migrator seams.</summary>
public static class PostgresPersistenceServiceCollectionExtensions
{
    /// <summary>Registers the full Postgres host floor for <typeparamref name="TContext"/> — resolves the connection string, builds the shared <see cref="NpgsqlDataSource"/>, registers the Dapper connection factory and audit interceptor, adds the snake_case-naming audited <see cref="DbContext"/>, and wires the embedded-resource bespoke migrator over the context's assembly.</summary>
    /// <typeparam name="TContext">The application <see cref="DbContext"/> to register.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration the connection string is resolved from.</param>
    /// <param name="configure">An optional hook to override <see cref="PostgresPersistenceOptions"/>.</param>
    /// <exception cref="InvalidOperationException">Neither the environment variable nor the configured key yields a non-blank connection string.</exception>
    public static IServiceCollection AddPostgresPersistence<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PostgresPersistenceOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new PostgresPersistenceOptions();
        configure?.Invoke(options);

        var connectionString = ResolveConnectionString(configuration, options);

        // DatabaseOptions backs AddNpgsqlDataSource (it reads IOptions<DatabaseOptions>); register both the record and IOptions.
        var databaseOptions = new DatabaseOptions { ConnectionString = connectionString };
        services.AddSingleton(databaseOptions);
        services.AddSingleton<IOptions<DatabaseOptions>>(Options.Create(databaseOptions));

        // Shared NpgsqlDataSource (+ DbDataSource) consumed by EF Core and Dapper; the connection factory rides the DbDataSource.
        services.AddNpgsqlDataSource();
        services.AddDataSourceConnectionFactory();
        services.AddEfCoreAuditInterceptor();

        services.AddDbContext<TContext>((serviceProvider, optionsBuilder) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            optionsBuilder
                .UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention()
                .UseAuditInterceptor(serviceProvider);
        });

        // The bespoke SQL migrator owns the schema; EF is a pure mapper over it.
        services.AddDatabaseBespokeMigrations(typeof(TContext).Assembly);

        return services;
    }

    /// <summary>Resolves the connection string, with the environment variable winning over configuration.</summary>
    /// <param name="configuration">The configuration read for the fallback value.</param>
    /// <param name="options">The options carrying the config key and environment-variable name.</param>
    /// <exception cref="InvalidOperationException">Both sources are blank.</exception>
    private static string ResolveConnectionString(IConfiguration configuration, PostgresPersistenceOptions options)
    {
        var fromConfig = configuration[options.ConnectionStringConfigKey];
        var fromEnvironment = Environment.GetEnvironmentVariable(options.ConnectionStringEnvironmentVariable);
        var connectionString = string.IsNullOrWhiteSpace(fromEnvironment) ? fromConfig : fromEnvironment;

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Database connection string not found. Set env var '{options.ConnectionStringEnvironmentVariable}' or configuration '{options.ConnectionStringConfigKey}'.");

        return connectionString;
    }
}
