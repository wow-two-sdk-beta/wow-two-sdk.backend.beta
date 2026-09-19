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
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats.Transports;

/// <summary>
/// Transports events from NATS JetStream by provisioning the stream and durable consumer, then driving an async
/// consume loop into the pipeline. The consumer's subject filter is the mirror image of the send path's subject
/// resolution: every routing key <see cref="ITopologyService"/> declares for this process, mapped through the same
/// <see cref="NatsSubjectNameMapper"/>.
/// </summary>
internal sealed partial class NatsReceiveTransport(
    NatsOptions options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology,
    ILogger<NatsReceiveTransport> logger,
    IReplyAddressService? replyAddresses = null,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    private readonly NatsTopologyBroker _topology = new();
    private readonly NatsConnection _connection = new(new NatsOpts { Url = options.Url });
    private NatsJSContext? _js;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        _js = new NatsJSContext(_connection);

        var subjects = ResolveConsumeSubjects();
        LogConsumeSubjects(string.Join(", ", subjects));

        await _topology.EnsureStreamAsync(_js, options, cancellationToken);
        await _topology.EnsureConsumerAsync(_js, options, subjects, cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = Task.Run(() => ConsumeLoopAsync(onMessage, _cts.Token), CancellationToken.None);
        _loop = loop;

        // ExecuteAsync's body — awaiting keeps stop→drain→release ordered; faults: Nats.md § Consume-loop faults.
        await loop;
    }

    /// <summary>Every subject this process consumes — <see cref="NatsOptions.Subject"/> always, plus each routed subject when routing is on.</summary>
    private List<string> ResolveConsumeSubjects()
    {
        var opt = options;
        var subjects = new List<string> { opt.Subject };
        if (!opt.RouteByDestination)
            return subjects;

        // One subject per consumed type, plus the endpoint's own name so an addressed send or reply arrives.
        foreach (var endpoint in topology.ConsumeEndpoints)
            foreach (var routingKey in endpoint.RoutingKeys)
                AddSubject(subjects, NatsSubjectNameMapper.Route(opt.Subject, routingKey));

        // A per-instance reply address is outside the topology — unfiltered, every request times out.
        if (replyAddresses is not null)
            AddSubject(subjects, NatsSubjectNameMapper.Route(opt.Subject, replyAddresses.ReplyAddress));

        return subjects;

        static void AddSubject(List<string> subjects, string subject)
        {
            if (subject.Length != 0 && !subjects.Contains(subject, StringComparer.Ordinal))
                subjects.Add(subject);
        }
    }

    private async Task ConsumeLoopAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        var consumer = await _js!.GetConsumerAsync(options.Stream, options.DurableConsumer, cancellationToken);
        try
        {
            await foreach (var message in consumer.ConsumeAsync<byte[]>(cancellationToken: cancellationToken))
            {
                var envelope = TryReconstruct(message);
                if (envelope is null)
                {
                    LogUnparseable();
                    // Don't silently drop — re-publish the raw message to the dead-letter subject, then ack.
                    var deadLetterHeaders = NatsWireFormatMapper.BuildDeadLetterHeaders(message.Headers, "unparseable", null);
                    await _js!.PublishAsync(options.DeadLetterSubject, message.Data ?? [], headers: deadLetterHeaders, cancellationToken: cancellationToken);
                    await message.AckAsync(cancellationToken: cancellationToken);
                    continue;
                }

                var context = new NatsReceiveContext(envelope, _js!, options.DeadLetterSubject, message);
                try
                {
                    // The pipeline settles via the context (ctx.Acknowledge on success / ctx.DeadLetter on exhaustion).
                    await onMessage(context, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogProcessingError(ex); // unsettled → JetStream redelivers after AckWait (deduped by the inbox)
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is { } cts)
            await cts.CancelAsync();

        if (_loop is { } loop)
        {
            try
            {
                await loop.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // stop timed out or loop cancelled — proceed to dispose
            }
            catch (Exception)
            {
                // The loop faulted and StartAsync already surfaced it; swallow so the release below still runs.
            }
        }

        await DisposeAsync();
    }

    private EventEnvelopeModel? TryReconstruct(NatsJSMsg<byte[]> message)
    {
        var headers = NatsWireFormatMapper.DecodeHeaders(message.Headers);
        if (!headers.TryGetValue(MessageHeaderConstants.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        if (message.Data is not { } data)
            return null;

        // Select on the declared content type or nothing — guessing JSON would route a legacy body to the wrong deserializer.
        var decoded = SerializerFor(ReadOptional(headers, MessageHeaderConstants.ContentType)).Deserialize(data, eventType);
        if (decoded.IsFailure(out _, out var body))
            return null;

        return new EventEnvelopeModel
        {
            MessageId = headers.TryGetValue(MessageHeaderConstants.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = message.Subject,
            DeliveryCount = (int)(message.Metadata?.NumDelivered ?? 1), // JetStream tracks redelivery natively
            ContentType = headers.TryGetValue(MessageHeaderConstants.ContentType, out var contentType) ? contentType : "application/json",
            PartitionKey = headers.TryGetValue(MessageHeaderConstants.PartitionKey, out var partitionKey) && !string.IsNullOrEmpty(partitionKey) ? partitionKey : null,
            ReplyTo = ReadOptional(headers, MessageHeaderConstants.ReplyTo),
            CorrelationId = ReadOptional(headers, MessageHeaderConstants.CorrelationId),
            ConversationId = ReadOptional(headers, MessageHeaderConstants.ConversationId),
            Headers = headers,
        };
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer when no registry is wired.</summary>
    private IMessageSerializer SerializerFor(string? contentType) => serializerRegistry?.Resolve(contentType) ?? serializer;

    public async ValueTask DisposeAsync()
    {
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        await _connection.DisposeAsync();
    }

    [LoggerMessage(EventId = 6501, Level = LogLevel.Warning, Message = "Discarding unparseable NATS message")]
    private partial void LogUnparseable();

    [LoggerMessage(EventId = 6502, Level = LogLevel.Error, Message = "NATS message processing failed; not acknowledged (will redeliver)")]
    private partial void LogProcessingError(Exception exception);

    [LoggerMessage(EventId = 6503, Level = LogLevel.Information, Message = "Consuming NATS subjects {Subjects}")]
    private partial void LogConsumeSubjects(string subjects);
}
