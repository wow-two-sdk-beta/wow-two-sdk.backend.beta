using DbUp;
using DbUp.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp.Extensions;

/// <summary>Selects the DbUp engine a host runs its migrations through.</summary>
/// <remarks>
/// Sqlite has no method here — the dbup-sqlite engine takes a connection object rather than a string, so a host
/// targeting it assigns <see cref="DbUpOptions.UpgradeEngineFactory"/> directly, e.g.
/// <c>cs =&gt; DeployChanges.To.SQLiteDatabase(new SharedConnection(new SQLiteConnection(cs)))</c>.
/// </remarks>
public static class DbUpExtensions
{
    /// <summary>Runs migrations through the Postgres engine.</summary>
    /// <param name="options">The options being configured.</param>
    public static DbUpOptions UsePostgres(this DbUpOptions options) =>
        With(options, cs => DeployChanges.To.PostgresqlDatabase(cs));

    /// <summary>Runs migrations through the SQL Server engine.</summary>
    /// <param name="options">The options being configured.</param>
    public static DbUpOptions UseSqlServer(this DbUpOptions options) =>
        With(options, cs => DeployChanges.To.SqlDatabase(cs));

    /// <summary>Runs migrations through the MySQL engine.</summary>
    /// <param name="options">The options being configured.</param>
    public static DbUpOptions UseMySql(this DbUpOptions options) =>
        With(options, cs => DeployChanges.To.MySqlDatabase(cs));

    /// <summary>Assigns the engine factory and hands the options back, so calls chain.</summary>
    /// <param name="options">The options being configured.</param>
    /// <param name="engineFactory">The engine builder for the chosen provider.</param>
    private static DbUpOptions With(DbUpOptions options, Func<string, UpgradeEngineBuilder> engineFactory)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.UpgradeEngineFactory = engineFactory;
        return options;
    }
}
