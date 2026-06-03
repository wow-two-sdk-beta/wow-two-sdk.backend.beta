namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Configuration for a database connection.</summary>
/// <example>Database</example>
public sealed record DatabaseOptions
{
    /// <summary>Gets the database connection string.</summary>
    /// <example>Host=localhost;Port=5432;Database=app;Username=postgres;Password=postgres</example>
    public required string ConnectionString { get; init; }
}
