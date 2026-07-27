namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Content-type → <see cref="IMessageSerializer"/> map consulted on the receive path, so a message is decoded by the
/// serializer that produced it rather than by whichever one this service happens to send with.
/// </summary>
/// <remarks>
/// <para>
/// Every adapter stamps <see cref="Transport.MessageHeaders.ContentType"/> on send and reads it back into
/// <see cref="EventEnvelope.ContentType"/> on receive. Without this map that value was carried and ignored: the single
/// injected <see cref="IMessageSerializer"/> decoded every message, so a service consuming from two producers on
/// different formats mis-deserialized one of them silently.
/// </para>
/// <para>
/// Registration is additive. The map is built from every <see cref="IMessageSerializer"/> in the container, keyed by
/// the content type each one declares, and anything absent or unregistered falls back to <see cref="Default"/> — the
/// singular registered serializer. With one serializer registered (the shipped default) every lookup lands on it, so
/// behaviour is identical to having no map at all.
/// </para>
/// <para>
/// A second entry comes from <c>AddReceiveOnlyMessageSerializer&lt;T&gt;()</c>; <c>AddMessageSerializer&lt;T&gt;()</c>
/// swaps the default and so leaves the count at one. Both register under <see cref="IMessageSerializer"/>, which is what
/// puts them in the injected enumerable, and the default is the last of those descriptors — so it arrives here through
/// both parameters as one shared singleton, keyed once. It is not instantiated twice and cannot be displaced by an entry
/// declaring the same content type, because it is enumerated last.
/// </para>
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

        // Last registration wins a contested content type, matching the container's own rule for resolving the
        // singular IMessageSerializer — so the map and Default cannot disagree about who owns a format.
        foreach (var serializer in serializers)
            if (MediaType(serializer.ContentType) is { Length: > 0 } key)
                _byMediaType[key] = serializer;

        // Covers a hand-wired registry whose default was never in the enumerable. TryAdd rather than assign: a
        // serializer that explicitly declared this content type keeps it, and Default still catches everything else.
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
    /// The serializer for <paramref name="contentType"/>, or <see cref="Default"/> when it is absent or names a format
    /// nothing registered.
    /// </summary>
    /// <remarks>
    /// Falling back rather than failing is deliberate. An unknown content type most often means an older producer that
    /// stamped nothing, or one on a format this service does not consume; letting <see cref="Default"/> try it keeps
    /// the pre-existing behaviour, and a body it cannot decode still dead-letters through the adapter's unparseable
    /// path instead of taking the consumer down.
    /// </remarks>
    /// <param name="contentType">The content type read off the wire; null or empty when the producer stamped none.</param>
    public IMessageSerializer Resolve(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return Default;

        // Adapters stamp a bare media type, so the header almost always matches a key verbatim and needs no normalizing.
        if (_byMediaType.TryGetValue(contentType, out var direct))
            return direct;

        return MediaType(contentType) is { Length: > 0 } key && _byMediaType.TryGetValue(key, out var normalized) ? normalized : Default;
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
