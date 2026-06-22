namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef;

/// <summary>Options for the EF Migrations startup runner.</summary>
public sealed record EfMigrationsOptions
{
    /// <summary>Gets a value indicating whether the runner is enabled. Default <c>true</c> — flip to <c>false</c> in production when migrations are applied out-of-band (CI step, ops job).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the maximum number of attempts to connect before giving up. Default 10. Mitigates the classic "DB not ready yet" Docker startup race.</summary>
    public int MaxConnectAttempts { get; init; } = 10;

    /// <summary>Gets the delay between connect attempts. Default 2s.</summary>
    public TimeSpan ConnectRetryDelay { get; init; } = TimeSpan.FromSeconds(2);
}
