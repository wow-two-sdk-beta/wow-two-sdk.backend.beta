namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation;

/// <summary>Holds options for listening to cache invalidations over PostgreSQL notifications.</summary>
public sealed record PostgresCacheInvalidationOptions
{
    /// <summary>Gets or sets the connection string of the database every host writes to.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the notification channel. Defaults to <see cref="CacheInvalidationConstants.DefaultChannel"/>.</summary>
    public string Channel { get; set; } = CacheInvalidationConstants.DefaultChannel;

    /// <summary>Gets or sets the wait before reconnecting a lost listener. Defaults to five seconds.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}
