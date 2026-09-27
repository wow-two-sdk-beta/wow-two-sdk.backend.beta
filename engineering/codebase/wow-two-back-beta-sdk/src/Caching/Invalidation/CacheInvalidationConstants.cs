namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation;

/// <summary>Holds the fixed channel and payload vocabulary of cross-host cache invalidation.</summary>
public static class CacheInvalidationConstants
{
    /// <summary>The PostgreSQL notification channel used when none is configured.</summary>
    public const string DefaultChannel = "wow2_cache_invalidation";

    /// <summary>The payload prefix naming one cache key.</summary>
    public const string KeyPrefix = "key:";

    /// <summary>The payload prefix naming one cache tag.</summary>
    public const string TagPrefix = "tag:";

    /// <summary>The application name the listener connection reports, when none is configured.</summary>
    public const string ListenerApplicationName = "wow2-cache-invalidation";

    /// <summary>The largest payload PostgreSQL delivers, in bytes.</summary>
    public const int MaxPayloadBytes = 7999;
}
