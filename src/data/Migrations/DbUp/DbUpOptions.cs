using System.Reflection;
using DbUp.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp;

/// <summary>
/// Options for the DbUp startup runner.
/// </summary>
public sealed record DbUpOptions
{
    /// <summary>Gets a value indicating whether the runner is enabled. Default <c>true</c>.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the assembly to scan for embedded <c>.sql</c> scripts. Defaults to the entry
    /// assembly when null.
    /// </summary>
    public Assembly? ScriptsAssembly { get; init; }

    /// <summary>
    /// Gets an optional namespace prefix to filter scripts (e.g. <c>"App.Migrations.Scripts."</c>).
    /// </summary>
    public string? ScriptsNamespacePrefix { get; init; }

    /// <summary>
    /// Gets a delegate that builds the <see cref="UpgradeEngineBuilder"/> for the target provider.
    /// Required — picks Postgres / SqlServer / MySql / Sqlite.
    /// </summary>
    public Func<string, UpgradeEngineBuilder>? UpgradeEngineFactory { get; init; }

    /// <summary>Gets the connection string for the target database.</summary>
    public string ConnectionString { get; init; } = string.Empty;
}
