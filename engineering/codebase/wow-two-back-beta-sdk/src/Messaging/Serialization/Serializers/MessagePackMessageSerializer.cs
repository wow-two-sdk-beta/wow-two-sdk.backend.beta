using MessagePack;
using MessagePack.Resolvers;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

/// <summary>Serializes event bodies as MessagePack binary data and decodes them into the requested type.</summary>
/// <remarks>
///   - defaults to <see cref="ContractlessStandardResolver"/>, so a plain event record needs no <c>[MessagePackObject]</c>
///   - Never pass a typeless resolver — it instantiates whatever type the sender names
///   - turning compression on changes the bytes on the wire, so every producer and consumer of a stream switches together
/// </remarks>
public sealed class MessagePackMessageSerializer : IMessageSerializer
{
    /// <summary>Holds the content type this serializer stamps — the de-facto MessagePack media type.</summary>
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
    public Result<object> Deserialize(ReadOnlySpan<byte> data, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(bodyType);

        if (data.IsEmpty)
        {
            return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                $"An empty payload cannot be decoded into '{bodyType.Name}'."));
        }

        try
        {
            // MessagePack's entry points take ReadOnlyMemory or a ReadOnlySequence, so the span is copied once — seam note in serialization.md.
            var body = MessagePackSerializer.Deserialize(bodyType, data.ToArray(), _options);

            return body is null
                ? Result<object>.Fail(AppErrorFactory.SerializationFailed(
                    $"The payload decoded to null for '{bodyType.Name}'."))
                : Result<object>.Ok(body);
        }
        catch (MessagePackSerializationException exception)
        {
            return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                $"The payload is not valid MessagePack for '{bodyType.Name}'.", exception));
        }
    }
}
