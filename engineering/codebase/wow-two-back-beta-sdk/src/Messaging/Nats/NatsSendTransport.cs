using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>
/// NATS JetStream <see cref="ISendTransport"/> — publishes JSON-serialized events to a stream subject. The subject
/// comes from <see cref="ITopologyService"/> once <see cref="NatsOptions.RouteByDestination"/> is on: the message
/// type's stable token for a publish, the caller's address for an explicit send. Ensures the stream exists on first send.
/// </summary>
internal sealed class NatsSendTransport(
    IOptions<NatsOptions> options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology) : ISendTransport, IAsyncDisposable
{
    private readonly NatsTopologyBroker _topology = new();
    private readonly NatsConnection _connection = new(new NatsOpts { Url = options.Value.Url });
    private readonly SemaphoreSlim _provisionGate = new(1, 1);
    private NatsJSContext? _js;
    private bool _streamReady;

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var js = await EnsureStreamAsync(cancellationToken);
        var body = envelope.ToWireBody(serializer);

        // The type token names the shape actually on the wire, which is what the receiver deserializes into.
        var headers = NatsWireFormatMapper.BuildHeaders(envelope, typeResolver.ToTypeToken(envelope.WireBodyType), serializer.ContentType);

        await js.PublishAsync(ResolveSubject(envelope), body, headers: headers, cancellationToken: cancellationToken);
    }

    /// <summary>The subject this envelope is published to — <see cref="NatsOptions.Subject"/>, or the topology's resolved key when routing is on.</summary>
    private string ResolveSubject(EventEnvelope envelope)
        => options.Value.RouteByDestination
            ? NatsSubjectNameMapper.Route(options.Value.Subject, topology.ResolveRoutingKey(envelope))
            : options.Value.Subject;

    private async ValueTask<NatsJSContext> EnsureStreamAsync(CancellationToken cancellationToken)
    {
        _js ??= new NatsJSContext(_connection);
        if (_streamReady)
            return _js;

        await _provisionGate.WaitAsync(cancellationToken);
        try
        {
            if (!_streamReady)
            {
                await _topology.EnsureStreamAsync(_js, options.Value, cancellationToken);
                _streamReady = true;
            }
        }
        finally
        {
            _provisionGate.Release();
        }

        return _js;
    }

    public async ValueTask DisposeAsync()
    {
        _provisionGate.Dispose();
        await _connection.DisposeAsync();
    }
}
