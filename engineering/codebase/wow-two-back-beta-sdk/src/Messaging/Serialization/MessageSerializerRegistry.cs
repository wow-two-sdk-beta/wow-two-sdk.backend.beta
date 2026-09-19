using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Binds content types to <see cref="IMessageSerializer"/> implementations on the receive path, so a message is decoded by the
/// serializer that produced it rather than by whichever one this service happens to send with.
/// </summary>
/// <remarks>
///   - built from every registered <see cref="IMessageSerializer"/>, keyed by the content type each declares
///   - add a receive-only format with <c>AddReceiveOnlyMessageSerializer&lt;T&gt;()</c>
///   - a receive-only format never displaces <see cref="Default"/>
/// </remarks>
public sealed class MessageSerializerRegistry
{
    private readonly Dictionary<string, IMessageSerializer> _byMediaType;

    /// <summary>Build the map from the container's serializers.</summary>
    /// <param name="defaultSerializer">The serializer used to send, and the fallback for an absent or unregistered content type.</param>
    /// <param name="serializers">Every registered serializer, each keyed by the content type it declares.</param>
    public MessageSerializerRegistry(IMessageSerializer defaultSerializer, IEnumerable<IMessageSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(defaultSerializer);
        ArgumentNullException.ThrowIfNull(serializers);

        Default = defaultSerializer;
        _byMediaType = new Dictionary<string, IMessageSerializer>(StringComparer.OrdinalIgnoreCase);

        // Last registration wins a contested content type, matching the container's rule for the singular IMessageSerializer.
        foreach (var serializer in serializers)
            if (MediaType(serializer.ContentType) is { Length: > 0 } key)
                _byMediaType[key] = serializer;

        // Default takes the key only when no registered serializer declared it — covers a hand-wired registry.
        if (MediaType(defaultSerializer.ContentType) is { Length: > 0 } defaultKey)
            _byMediaType.TryAdd(defaultKey, defaultSerializer);
    }

    /// <summary>Build a single-serializer map — the shape the SDK ships with, and the convenient one to hand-wire in a test.</summary>
    /// <param name="defaultSerializer">The only serializer; every content type resolves to it.</param>
    public MessageSerializerRegistry(IMessageSerializer defaultSerializer)
        : this(defaultSerializer, [defaultSerializer])
    {
    }

    /// <summary>The serializer used to send, and the fallback for a message whose content type is absent or unregistered.</summary>
    public IMessageSerializer Default { get; }

    /// <summary>
    /// Gets the serializer for <paramref name="contentType"/>, or <see cref="Default"/> when it is absent.
    /// </summary>
    /// <remarks>
    ///   - an absent content type means an older producer that stamped none
    ///   - a body <see cref="Default"/> cannot decode dead-letters through the adapter's unparseable path
    /// </remarks>
    /// <param name="contentType">The content type read off the wire; null or empty when the producer stamped none.</param>
    /// <exception cref="InvalidOperationException">No serializer is bound to the declared content type.</exception>
    public IMessageSerializer Resolve(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return Default;

        // Adapters stamp a bare media type, so the header almost always matches a key verbatim and needs no normalizing.
        if (_byMediaType.TryGetValue(contentType, out var direct))
            return direct;

        var key = MediaType(contentType);
        if (key is { Length: > 0 } && _byMediaType.TryGetValue(key, out var normalized))
            return normalized;

        throw new InvalidOperationException(
            $"No message serializer is registered for content type '{contentType}'. "
            + "Register a matching serializer with AddReceiveOnlyMessageSerializer<TSerializer>().");
    }

    /// <summary>The bare media type — parameters (<c>; charset=utf-8</c>) and surrounding space removed; null when nothing is left.</summary>
    private static string? MediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        var span = contentType.AsSpan();
        var separator = span.IndexOf(';');
        if (separator >= 0)
            span = span[..separator];

        span = span.Trim();
        return span.IsEmpty ? null : span.ToString();
    }
}
