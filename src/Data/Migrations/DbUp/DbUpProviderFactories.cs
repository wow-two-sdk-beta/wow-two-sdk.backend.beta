using DbUp;
using DbUp.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp;

/// <summary>
/// Provider factory shortcuts for the supported DbUp engines.
/// </summary>
/// <remarks>
/// Sqlite is intentionally omitted — the dbup-sqlite engine takes a connection object rather
/// than a string. Consumers targeting Sqlite should set
/// <see cref="DbUpOptions.UpgradeEngineFactory"/> themselves, e.g.
/// <c>cs =&gt; DeployChanges.To.SQLiteDatabase(new SharedConnection(new SQLiteConnection(cs)))</c>.
/// </remarks>
public static class DbUpProviderFactories
{
    /// <summary>Postgres factory — pass as <see cref="DbUpOptions.UpgradeEngineFactory"/>.</summary>
    public static Func<string, UpgradeEngineBuilder> Postgres => cs => DeployChanges.To.PostgresqlDatabase(cs);

    /// <summary>SqlServer factory.</summary>
    public static Func<string, UpgradeEngineBuilder> SqlServer => cs => DeployChanges.To.SqlDatabase(cs);

    /// <summary>MySql factory.</summary>
    public static Func<string, UpgradeEngineBuilder> MySql => cs => DeployChanges.To.MySqlDatabase(cs);
}
