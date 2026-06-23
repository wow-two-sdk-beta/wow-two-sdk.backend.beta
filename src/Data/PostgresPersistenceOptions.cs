namespace WoW.Two.Sdk.Backend.Beta.Data;

/// <summary>Options for the one-call Postgres persistence bundle — controls where the connection string is resolved from.</summary>
public sealed class PostgresPersistenceOptions
{
    /// <summary>Gets or sets the configuration key the connection string is read from. Default <c>DatabaseOptions:ConnectionString</c>.</summary>
    public string ConnectionStringConfigKey { get; set; } = "DatabaseOptions:ConnectionString";

    /// <summary>Gets or sets the environment variable that overrides the configured connection string when set. Default <c>DB_CONNECTION</c>.</summary>
    public string ConnectionStringEnvironmentVariable { get; set; } = "DB_CONNECTION";
}
