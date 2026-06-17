namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Represents one applied migration recorded in the history table, mapped by Dapper (snake_case).</summary>
/// <example>An applied baseline migration.</example>
public sealed class MigrationHistoryEntry
{
    /// <summary>Gets or sets the migration ordinal (primary key).</summary>
    public int Ordinal { get; set; }

    /// <summary>Gets or sets the version label active when the migration was applied.</summary>
    /// <example>v1.0</example>
    public string Version { get; set; } = "";

    /// <summary>Gets or sets the migration name.</summary>
    /// <example>baseline</example>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the normalized Apply script checksum recorded at apply time.</summary>
    /// <example>SHA-256 hex digest</example>
    public string Checksum { get; set; } = "";

    /// <summary>Gets or sets the time the migration was applied.</summary>
    /// <example>2026-06-13 09:00 UTC</example>
    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>Gets or sets which host applied the migration.</summary>
    /// <example>startup</example>
    public string AppliedBy { get; set; } = "";

    /// <summary>Gets or sets the apply duration in milliseconds.</summary>
    /// <example>42</example>
    public int ExecutionMs { get; set; }
}
