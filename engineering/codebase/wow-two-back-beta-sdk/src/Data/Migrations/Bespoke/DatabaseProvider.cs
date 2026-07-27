namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Defines the database engine a migration run targets, selecting the matching SQL dialect.</summary>
/// <example>Set on <see cref="MigrationOptions.Provider"/> to pick the dialect.</example>
public enum DatabaseProvider
{
    /// <summary>PostgreSQL.</summary>
    /// <example>Targets a Postgres database.</example>
    Postgres,

    /// <summary>SQLite — file-based or in-memory.</summary>
    /// <example>Targets a SQLite database file.</example>
    Sqlite,
}
