namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

/// <summary>Defines the database engine a migration run targets, selecting the matching SQL dialect.</summary>
/// <example>Set on <see cref="MigrationOptions.Provider"/> to pick the dialect.</example>
public enum DatabaseProvider
{
    /// <summary>PostgreSQL — the only supported provider today.</summary>
    /// <example>Targets a Postgres database.</example>
    Postgres,
}
