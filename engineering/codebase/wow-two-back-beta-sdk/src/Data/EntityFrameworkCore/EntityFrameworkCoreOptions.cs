namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Holds configuration for a DbContext registered via <c>AddEntityFrameworkCore&lt;T&gt;</c>.</summary>
public sealed record EntityFrameworkCoreOptions
{
    /// <summary>Gets a value indicating whether DbContext pooling is enabled. Default <c>true</c>.</summary>
    public bool UsePooling { get; set; } = true;

    /// <summary>Gets the DbContext pool size when <see cref="UsePooling"/> is enabled. Default 1024.</summary>
    public int PoolSize { get; set; } = 1024;

    /// <summary>Gets whether sensitive parameter data is logged. <c>null</c> enables it only in Development.</summary>
    public bool? EnableSensitiveDataLogging { get; set; }

    /// <summary>Gets whether detailed errors are enabled. <c>null</c> enables them only in Development.</summary>
    public bool? EnableDetailedErrors { get; set; }

    /// <summary>Gets a value indicating whether no-tracking is the default query behavior. Default <c>false</c>.</summary>
    public bool NoTrackingByDefault { get; set; }
}
