using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Serializes an event body to/from the wire. Pluggable — the default is System.Text.Json (through the SDK's
/// <see cref="JsonOptionsConstants"/>); register another (MessagePack, Protobuf, …) via <c>AddMessageSerializer</c>.
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

    /// <summary>Deserialize <paramref name="data"/> into <paramref name="bodyType"/>, or fail with the reason it could not be decoded.</summary>
    /// <remarks>
    ///   - an empty payload, a malformed body and a decode yielding null all fail with <see cref="AppErrorType.SerializationFailed"/>
    ///   - Never throw for an undecodable body — a throw escapes the consume loop and stops the subscription
    ///   - take the unparseable path on a failure — DLQ topic, nack, native dead-letter, or an outbox row marked failed
    /// </remarks>
    /// <param name="data">The serialized bytes.</param>
    /// <param name="bodyType">The target runtime type.</param>
    Result<object> Deserialize(ReadOnlySpan<byte> data, Type bodyType);
}
