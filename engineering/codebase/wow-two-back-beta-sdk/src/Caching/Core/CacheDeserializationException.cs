namespace WoW.Two.Sdk.Backend.Beta.Caching.Core;

/// <summary>Represents a cached entry that no longer deserializes into the type a read asked for.</summary>
public sealed class CacheDeserializationException : Exception
{
    /// <summary>Creates the exception for <paramref name="valueType"/>, keyed when the key is known.</summary>
    /// <param name="key">The cache key, or null inside a serializer that never sees it.</param>
    /// <param name="valueType">The type the read asked for.</param>
    /// <param name="innerException">The serializer's failure.</param>
    public CacheDeserializationException(string? key, Type valueType, Exception innerException)
        : base($"The cached entry{(key is null ? string.Empty : $" '{key}'")} does not deserialize into {valueType?.Name}.", innerException)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        Key = key;
        ValueType = valueType;
    }

    /// <summary>Creates the exception with a message.</summary>
    public CacheDeserializationException()
        : base("A cached entry does not deserialize.")
    {
        ValueType = typeof(object);
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public CacheDeserializationException(string message)
        : base(message)
    {
        ValueType = typeof(object);
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public CacheDeserializationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValueType = typeof(object);
    }

    /// <summary>The cache key, when known.</summary>
    public string? Key { get; }

    /// <summary>The type the read asked for.</summary>
    public Type ValueType { get; }
}
