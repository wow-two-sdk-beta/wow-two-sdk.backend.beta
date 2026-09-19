using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

/// <summary>Serializes event bodies as UTF-8 JSON using System.Text.Json with configured options and decodes them into the requested type.</summary>
public sealed class SystemTextJsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Create the serializer with optional overriding options (defaults to <see cref="JsonOptionsConstants.Default"/>).</summary>
    /// <param name="options">JSON options; null uses the SDK default preset.</param>
    public SystemTextJsonMessageSerializer(JsonSerializerOptions? options = null) => _options = options ?? JsonOptionsConstants.Default;

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
            var body = JsonSerializer.Deserialize(data, bodyType, _options);

            // A payload of the four bytes `null` decodes without error and yields nothing to hand a handler.
            return body is null
                ? Result<object>.Fail(AppErrorFactory.SerializationFailed(
                    $"The payload decoded to null for '{bodyType.Name}'."))
                : Result<object>.Ok(body);
        }
        catch (JsonException exception)
        {
            return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                $"The payload is not valid JSON for '{bodyType.Name}'.", exception));
        }
    }
}
