namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Refers to the database engine a migration run targets, selecting the matching SQL dialect.</summary>
public enum DatabaseProvider
{
    /// <summary>PostgreSQL.</summary>
    Postgres,

    /// <summary>SQLite — file-based or in-memory.</summary>
    Sqlite,
}
