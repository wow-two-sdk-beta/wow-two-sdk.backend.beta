using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides registration extensions for the SQL migrator — an embedded-resource source for runtime hosts, a filesystem source for the CLI.</summary>
public static class MigrationServiceCollectionExtensions
{
    /// <summary>Adds the embedded-resource SQL migrator (the runtime default — schema ships in the binary).</summary>
    /// <remarks>The host passes the assembly that embeds the <c>Migrations/NNN-name/*.sql</c> resources; requires an <see cref="IDbConnectionFactory"/> registered alongside.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="sqlAssembly">The assembly that embeds the migration SQL resources.</param>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/> (e.g. enable rollback for dev hosts).</param>
    public static IServiceCollection AddDatabaseBespokeMigrations(
        this IServiceCollection services, Assembly sqlAssembly, Action<MigrationOptions>? configure = null) =>
        services.AddDatabaseBespokeMigrations(_ => new EmbeddedResourceMigrationSource(sqlAssembly), configure);

    /// <summary>Adds the filesystem SQL migrator over an on-disk migrations root (the CLI and dev default — schema is edited live).</summary>
    /// <remarks>Wire from the CLI host; reads <c>{root}/NNN-name/{Apply,Rollback}.sql</c> and requires an <see cref="IDbConnectionFactory"/> registered alongside.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="migrationsRoot">The on-disk folder containing the <c>NNN-name</c> migration directories.</param>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/> (e.g. enable rollback for dev hosts).</param>
    public static IServiceCollection AddDatabaseBespokeMigrations(
        this IServiceCollection services, string migrationsRoot, Action<MigrationOptions>? configure = null) =>
        services.AddDatabaseBespokeMigrations(_ => new FileSystemMigrationSource(migrationsRoot), configure);

    /// <summary>Adds the SQL migrator over a caller-supplied source factory — the shared core both public overloads delegate to.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="sourceFactory">A factory that builds the migration source from the provider.</param>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/>.</param>
    private static IServiceCollection AddDatabaseBespokeMigrations(
        this IServiceCollection services, Func<IServiceProvider, IMigrationSource> sourceFactory, Action<MigrationOptions>? configure)
    {
        services.AddOptions<MigrationOptions>().Configure(options => configure?.Invoke(options));
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<MigrationOptions>>().Value);

        services.AddSingleton(sourceFactory);
        // Resolved rather than captured, so the dialect reads the provider AFTER Configure has run.
        services.AddSingleton<IMigrationDialect>(serviceProvider =>
            CreateDialect(serviceProvider.GetRequiredService<MigrationOptions>().Provider));
        services.AddSingleton<IMigrationHistoryRepository, MigrationHistoryRepository>();
        services.AddSingleton<IMigrationRunnerService, MigrationRunnerService>();

        return services;
    }

    /// <summary>Selects the dialect implementation for the configured provider.</summary>
    /// <param name="provider">The database provider to resolve a dialect for.</param>
    /// <exception cref="ArgumentOutOfRangeException">The provider has no registered dialect.</exception>
    private static IMigrationDialect CreateDialect(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Postgres => new PostgresMigrationDialect(),
        DatabaseProvider.Sqlite => new SqliteMigrationDialect(),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "No migration dialect for this provider."),
    };
}
