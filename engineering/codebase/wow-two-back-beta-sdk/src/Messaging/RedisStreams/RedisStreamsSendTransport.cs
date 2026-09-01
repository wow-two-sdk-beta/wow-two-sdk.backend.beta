using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>
/// Redis Streams <see cref="ISendTransport"/> — <c>XADD</c>s a JSON-serialized event as a flat field/value entry. The
/// stream key comes from <see cref="ITopologyService"/> once <see cref="RedisStreamsOptions.RouteByDestination"/> is
/// on: the message type's stable token for a publish, the caller's address for an explicit send.
/// </summary>
internal sealed class RedisStreamsSendTransport(
    IOptions<RedisStreamsOptions> options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology,
    RedisStreamsConnection connection) : ISendTransport
{
    private readonly RedisStreamsTopologyBroker _topology = new();
    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var database = await connection.GetDatabaseAsync(cancellationToken);
        var body = envelope.ToWireBody(serializer);

        // WireBodyType, not BodyType — the receiver deserializes into this token; stream resolution stays on BodyType.
        var fields = RedisStreamsWireFormatMapper.BuildEntry(envelope, body, typeResolver.ToTypeToken(envelope.WireBodyType), serializer.ContentType);

        // A null messageId lets Redis assign a monotonic entry id; the SDK's MessageId rides a field instead.
        await database.StreamAddAsync(
            ResolveStream(envelope),
            fields,
            messageId: null,
            maxLength: options.Value.MaxLength,
            useApproximateMaxLength: options.Value.UseApproximateMaxLength);
    }

    /// <summary>
    /// The stream this envelope is added to. Off (the default) every message rides
    /// <see cref="RedisStreamsOptions.Stream"/>. On, the topology decides — and it is the topology, not the raw
    /// destination, for the same reason the RabbitMQ adapter routes on the resolved key: a publish has to land on the
    /// type's own stream, which is what a consumer of that type reads, while an explicit send has to land on the
    /// addressed endpoint's stream so it reaches that endpoint alone.
    /// </summary>
    private string ResolveStream(EventEnvelope envelope)
        => options.Value.RouteByDestination
            ? RedisStreamNameMapper.Route(options.Value.Stream, topology.ResolveRoutingKey(envelope))
            : options.Value.Stream;
}
