using System.Collections.Concurrent;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Serializes an event body to/from the wire. Pluggable — the default is System.Text.Json (through the SDK's
/// <see cref="JsonOptionsPresets"/>); register another (MessagePack, Protobuf, …) via <c>AddMessageSerializer</c>.
/// The transport stamps <see cref="ContentType"/> as a header so the receiver can select the matching serializer.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>The content type this serializer produces (e.g. <c>application/json</c>).</summary>
    string ContentType { get; }

    /// <summary>Serialize <paramref name="body"/> (of runtime type <paramref name="bodyType"/>) to bytes.</summary>
    /// <param name="body">The event body.</param>
    /// <param name="bodyType">The runtime type of the body.</param>
    byte[] Serialize(object body, Type bodyType);

    /// <summary>Deserialize <paramref name="data"/> into <paramref name="bodyType"/>; null when the payload is empty.</summary>
    /// <remarks>
    /// <para>
    /// An empty <paramref name="data"/> MUST return null rather than throw, and every shipped implementation obeys
    /// this. Null is the seam's "this body is not decodable" signal and every caller already converts it into an
    /// explicit failure — the adapters' <c>TryReconstruct</c> returns a null envelope and takes the unparseable path
    /// (DLQ topic / nack / native dead-letter), <c>ClaimCheck</c> throws <c>ClaimCheckPayloadException</c>, and the
    /// outbox dispatcher marks the row failed. No caller hands a null body to a handler.
    /// </para>
    /// <para>
    /// Throwing here is not the louder alternative, it is the fatal one: <c>TryReconstruct</c> is called outside the
    /// adapters' try/catch, so an exception escapes the consume loop and stops the subscription instead of costing one
    /// message. A body that is present but malformed is a different case and still throws.
    /// </para>
    /// </remarks>
    /// <param name="data">The serialized bytes.</param>
    /// <param name="bodyType">The target runtime type.</param>
    object? Deserialize(ReadOnlySpan<byte> data, Type bodyType);
}

/// <summary>Default <see cref="IMessageSerializer"/> — System.Text.Json routed through the SDK's shared <see cref="JsonOptionsPresets"/> so producer and consumer share one casing/enum/null policy.</summary>
public sealed class SystemTextJsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Create the serializer with optional overriding options (defaults to <see cref="JsonOptionsPresets.Default"/>).</summary>
    /// <param name="options">JSON options; null uses the SDK default preset.</param>
    public SystemTextJsonMessageSerializer(JsonSerializerOptions? options = null) => _options = options ?? JsonOptionsPresets.Default;

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public byte[] Serialize(object body, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(bodyType);
        return JsonSerializer.SerializeToUtf8Bytes(body, bodyType, _options);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The empty check is this implementation's own: <see cref="JsonSerializer"/> throws <see cref="JsonException"/>
    /// ("no JSON tokens") on an empty span, which broke the interface's empty-payload contract that MessagePack and
    /// CloudEvents both honour. A non-empty but malformed body still throws — only genuine emptiness returns null, and
    /// a payload of the four bytes <c>null</c> already returned null here before this check existed.
    /// </remarks>
    public object? Deserialize(ReadOnlySpan<byte> data, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(bodyType);
        return data.IsEmpty ? null : JsonSerializer.Deserialize(data, bodyType, _options);
    }
}

/// <summary>
/// Maps an event CLR type to/from a stable wire token, decoupled from the assembly-qualified name (which breaks
/// across services and on any rename/assembly-move — the AQN then fails to resolve and the message is lost). Pluggable
/// via <c>AddMessageTypeResolver</c>.
/// </summary>
public interface IMessageTypeResolver
{
    /// <summary>The stable wire token for <paramref name="type"/> (survives assembly rename/version changes).</summary>
    /// <param name="type">The event type.</param>
    string ToTypeToken(Type type);

    /// <summary>Resolve a wire token back to a CLR type, or null when unknown (caller should dead-letter, not drop).</summary>
    /// <param name="token">The wire token.</param>
    Type? ResolveType(string token);
}

/// <summary>
/// Registration-time map of event types ↔ their stable wire tokens (+ aliases for post-rename compatibility).
/// Populated by the handler scan (<c>AddEventHandlersFromAssemblies</c>) and consulted by <see cref="DefaultMessageTypeResolver"/>.
/// </summary>
public sealed class MessageTypeRegistry
{
    private readonly ConcurrentDictionary<Type, string> _tokenByType = new();
    private readonly ConcurrentDictionary<string, Type> _typeByToken = new(StringComparer.Ordinal);

    /// <summary>Register <paramref name="type"/> under its token (default = the type's full name). Idempotent.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="token">Explicit token; defaults to <see cref="Type.FullName"/>.</param>
    public void Register(Type type, string? token = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        var resolved = token ?? type.FullName ?? type.Name;
        _tokenByType[type] = resolved;
        _typeByToken[resolved] = type;
    }

    /// <summary>Map an alias token (e.g. an old contract name after a rename) to an existing type.</summary>
    /// <param name="alias">The alias token.</param>
    /// <param name="type">The current type.</param>
    public void AddAlias(string alias, Type type)
    {
        ArgumentException.ThrowIfNullOrEmpty(alias);
        ArgumentNullException.ThrowIfNull(type);
        _typeByToken[alias] = type;
    }

    /// <summary>Try to get the registered token for a type.</summary>
    /// <param name="type">The type.</param>
    /// <param name="token">The token, when found.</param>
    public bool TryGetToken(Type type, out string token) => _tokenByType.TryGetValue(type, out token!);

    /// <summary>Try to resolve a token to a registered type.</summary>
    /// <param name="token">The token.</param>
    /// <param name="type">The type, when found.</param>
    public bool TryGetType(string token, out Type type) => _typeByToken.TryGetValue(token, out type!);
}

/// <summary>
/// Default resolver: registered event types use their stable <see cref="Type.FullName"/> token; resolution tries the
/// registry, then aliases (both via <see cref="MessageTypeRegistry"/>), then falls back to <see cref="Type.GetType(string)"/>
/// / an assembly scan for assembly-qualified-name back-compat. Unregistered types emit their AQN.
/// </summary>
public sealed class DefaultMessageTypeResolver(MessageTypeRegistry registry) : IMessageTypeResolver
{
    private static readonly ConcurrentDictionary<string, Type?> FallbackCache = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string ToTypeToken(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return registry.TryGetToken(type, out var token) ? token : type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
    }

    /// <inheritdoc />
    public Type? ResolveType(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return registry.TryGetType(token, out var type) ? type : FallbackCache.GetOrAdd(token, ResolveFallback);
    }

    private static Type? ResolveFallback(string token)
    {
        var type = Type.GetType(token); // assembly-qualified-name back-compat
        if (type is not null)
            return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(token); // resolve by full name across loaded assemblies
            if (type is not null)
                return type;
        }

        return null;
    }
}
