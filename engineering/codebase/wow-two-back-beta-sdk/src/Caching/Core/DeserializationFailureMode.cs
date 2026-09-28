namespace WoW.Two.Sdk.Backend.Beta.Caching.Core;

/// <summary>Refers to what a cache read does with an entry it can no longer deserialize, such as one written before a shape change.</summary>
public enum DeserializationFailureMode
{
    /// <summary>Log a warning, evict the entry and run the factory, as for a miss.</summary>
    Drop,

    /// <summary>Surface a <see cref="CacheDeserializationException"/> to the caller.</summary>
    Throw,
}
