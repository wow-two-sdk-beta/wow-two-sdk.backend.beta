using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

using WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations.Services;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>Service-collection helper disabling the bespoke migrator's startup hook for tests whose schema comes from elsewhere (e.g. EF <c>EnsureCreated</c>).</summary>
public static class NoOpBespokeMigratorExtensions
{
    /// <summary>Replaces the bespoke migrator dialect and runner with no-ops so the startup migrate hook touches no database.</summary>
    /// <param name="services">The service collection to neuter.</param>
    public static IServiceCollection DisableBespokeMigrator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IMigrationDialect>();
        services.AddSingleton<IMigrationDialect, NoOpMigrationDialect>();
        services.RemoveAll<IMigrationRunnerService>();
        services.AddSingleton<IMigrationRunnerService, NoOpMigrationRunnerService>();
        return services;
    }
}
