using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Data;

/// <summary>Holds options for the one-call Postgres persistence bundle — controls where the connection string is resolved from.</summary>
public sealed record PostgresPersistenceOptions
{
    /// <summary>Gets or sets the configuration key the connection string is read from. Default <c>DatabaseSettings:ConnectionString</c>.</summary>
    public string ConnectionStringConfigKey { get; set; } = "DatabaseSettings:ConnectionString";

    /// <summary>Gets or sets the environment variable that overrides the configured connection string when set. Default <c>DB_CONNECTION</c>.</summary>
    public string ConnectionStringEnvironmentVariable { get; set; } = "DB_CONNECTION";

    /// <summary>
    /// Gets or sets a hook that tunes the bundled bespoke migrator — its version stamp, rollback switch or history
    /// table. Pair it with <see cref="MigrationVersionMapper.ToVersion"/> to stamp applied rows with the product version.
    /// </summary>
    public Action<MigrationOptions>? ConfigureMigrations { get; set; }
}
