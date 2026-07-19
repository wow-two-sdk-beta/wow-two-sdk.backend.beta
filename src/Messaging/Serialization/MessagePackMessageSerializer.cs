using MessagePack;
using MessagePack.Resolvers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Compact binary <see cref="IMessageSerializer"/> backed by MessagePack — typically 30–60% smaller on the wire than
/// JSON and materially cheaper to encode, at the cost of a payload no human can read off the broker. Swap it in with
/// <c>services.AddMessageSerializer&lt;MessagePackMessageSerializer&gt;()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Defaults to <see cref="ContractlessStandardResolver"/> so plain event records serialize with no
/// <c>[MessagePackObject]</c> annotation — a contract that works under the System.Text.Json default keeps working
/// after the swap. The resolver still honours <c>[MessagePackObject]</c>/<c>[Key]</c> where a contract declares them,
/// so integer-keyed contracts (smaller and faster still) remain available per type.
/// </para>
/// <para>
/// Security: the default options apply <see cref="MessagePackSecurity.UntrustedData"/> — broker payloads are untrusted
/// input, and this caps object-graph depth and uses collision-resistant hashing for map keys. The <em>typeless</em>
/// resolvers (<c>TypelessContractlessStandardResolver</c>) are deliberately NOT used: they embed CLR type names in the
/// payload and instantiate whatever the sender names, which is a remote-code-execution vector. Type identity belongs
/// to <see cref="IMessageTypeResolver"/> and travels in the <c>wt-event-type</c> header, not in the body.
/// </para>
/// <para>
/// Compression is off by default. <c>MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block)</c>
/// changes the bytes on the wire, so every producer and consumer of a stream has to be switched together.
/// </para>
/// </remarks>
public sealed class MessagePackMessageSerializer : IMessageSerializer
{
    /// <summary>The content type this serializer stamps — the de-facto MessagePack media type.</summary>
    /// <remarks>
    /// IANA also registers <c>application/vnd.msgpack</c>; <c>application/x-msgpack</c> is the spelling in widest
    /// production use. A peer expecting the other spelling needs the value aligned on both ends.
    /// </remarks>
    public const string MessagePackContentType = "application/x-msgpack";

    private static readonly MessagePackSerializerOptions DefaultOptions = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithSecurity(MessagePackSecurity.UntrustedData);

    private readonly MessagePackSerializerOptions _options;

    /// <summary>Create the serializer with optional overriding options (defaults to contractless + untrusted-data security).</summary>
    /// <param name="options">MessagePack options; null uses the SDK default (contractless resolver, untrusted-data security, no compression).</param>
    public MessagePackMessageSerializer(MessagePackSerializerOptions? options = null) => _options = options ?? DefaultOptions;

    /// <inheritdoc />
    public string ContentType => MessagePackContentType;

    /// <inheritdoc />
    public byte[] Serialize(object body, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(bodyType);
        return MessagePackSerializer.Serialize(bodyType, body, _options);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The <see cref="ReadOnlySpan{T}"/> parameter is copied once via <see cref="ReadOnlySpan{T}.ToArray"/>: MessagePack's
    /// entry points take <see cref="ReadOnlyMemory{T}"/> or a <see cref="System.Buffers.ReadOnlySequence{T}"/>, neither of
    /// which can wrap a span. See the seam note in <c>serialization.md</c>.
    /// </remarks>
    public object? Deserialize(ReadOnlySpan<byte> data, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(bodyType);
        return data.IsEmpty ? null : MessagePackSerializer.Deserialize(bodyType, data.ToArray(), _options);
    }
}
