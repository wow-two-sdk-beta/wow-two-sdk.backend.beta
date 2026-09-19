using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus.Transports;

/// <summary>
/// Transports envelopes from Azure Service Bus by provisioning the topology, then running a receive loop per endpoint
/// subscription (or per concurrent session, when <see cref="AzureServiceBusOptions.RequiresSession"/> is on) and routes
/// each message into the pipeline.
/// </summary>
/// <remarks>
///   - a message lock holds for the entity's configured duration, whoever settles it and from whichever thread
///   - a session receiver is released once its session drains, so off-loop settlement has a window there
///   - <see cref="AzureServiceBusCapabilities.SettlesInContext"/> reports false under sessions for that reason
/// </remarks>
internal sealed partial class AzureServiceBusReceiveTransport(
    AzureServiceBusConnection connection,
    AzureServiceBusOptions options,
    AzureServiceBusTopology topology,
    ITopologyService topologyProvider,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ILogger<AzureServiceBusReceiveTransport> logger,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    /// <summary>Pause before a receive loop retries after a non-transient failure. The client already retries transient faults, so reaching here means something the loop should back off from rather than spin on.</summary>
    private static readonly TimeSpan FaultBackoff = TimeSpan.FromSeconds(5);

    /// <summary>Receivers opened once per endpoint and kept for the life of the transport. Disposed in <see cref="StopAsync"/>, never when a loop exits — a pump worker may still be settling on one.</summary>
    private readonly ConcurrentDictionary<ServiceBusReceiver, byte> _receivers = new();

    private CancellationTokenSource? _cts;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        var endpoints = topologyProvider.ConsumeEndpoints;

        // StopAsync stops this transport by cancelling this source; anything awaiting a token it cannot reach hangs.
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        try
        {
            // No endpoints: nothing to provision or consume. Park rather than return, which reads as a clean shutdown.
            if (endpoints.Count == 0)
            {
                LogNoConsumeEndpoints();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return;
            }

            if (options.ProvisionEntities)
                await topology.ProvisionAsync(endpoints, token);

            var client = await connection.GetClientAsync(token);
            var topic = AzureServiceBusEntityNameMapper.Sanitize(options.Topic, AzureServiceBusEntityNameMapper.MaxTopicLength);

            var loops = new List<Task>();
            foreach (var endpoint in endpoints)
            {
                var subscription = AzureServiceBusEntityNameMapper.Sanitize(endpoint.Queue, AzureServiceBusEntityNameMapper.MaxSubscriptionLength);
                LogConsumingSubscription(subscription, options.RequiresSession);

                if (options.RequiresSession)
                {
                    // One accept loop per concurrent session: a session receiver holds exactly one session lock.
                    var sessions = Math.Max(1, options.MaxConcurrentSessions);
                    for (var index = 0; index < sessions; index++)
                        loops.Add(Task.Run(() => RunSessionLoopAsync(client, subscription, onMessage, token), CancellationToken.None));
                }
                else
                {
                    var receiver = client.CreateReceiver(topic, subscription, ReceiverOptions());
                    _receivers.TryAdd(receiver, 0);
                    loops.Add(Task.Run(() => RunReceiveLoopAsync(receiver, subscription, onMessage, token), CancellationToken.None));
                }
            }

            // This method is the hosted service's ExecuteAsync body: returning early reports the consumer as finished.
            await Task.WhenAll(loops);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Graceful shutdown; receivers stay open so handlers draining after this returns can still settle on them.
        }
    }

    private ServiceBusReceiverOptions ReceiverOptions() => new()
    {
        // PeekLock settles after processing; ReceiveAndDelete settles at delivery, losing a crash mid-handler silently.
        ReceiveMode = ServiceBusReceiveMode.PeekLock,
        PrefetchCount = Math.Max(0, options.PrefetchCount),
    };

    /// <summary>Pull batches from one subscription until cancelled, dispatching each message into the pipeline.</summary>
    private async Task RunReceiveLoopAsync(
        ServiceBusReceiver receiver,
        string subscription,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var batch = await receiver.ReceiveMessagesAsync(
                    Math.Max(1, options.MaxMessagesPerReceive),
                    options.ReceiveWaitTime,
                    cancellationToken);

                foreach (var message in batch)
                    await DispatchAsync(receiver, message, onMessage, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The loop must outlive a failed receive: faulting stops this subscription for the process's life.
                LogReceiveFailed(subscription, ex);
                await DelayAsync(FaultBackoff, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Lock one session at a time and drain it, then release it and accept the next. Releasing on idle rather than
    /// holding the lock is what stops a small pool of accept loops from pinning itself to quiet sessions while busy
    /// ones wait.
    /// </summary>
    private async Task RunSessionLoopAsync(
        ServiceBusClient client,
        string subscription,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        var topic = AzureServiceBusEntityNameMapper.Sanitize(options.Topic, AzureServiceBusEntityNameMapper.MaxTopicLength);

        while (!cancellationToken.IsCancellationRequested)
        {
            ServiceBusSessionReceiver? receiver = null;
            try
            {
                receiver = await client.AcceptNextSessionAsync(
                    topic,
                    subscription,
                    new ServiceBusSessionReceiverOptions
                    {
                        ReceiveMode = ServiceBusReceiveMode.PeekLock,
                        PrefetchCount = Math.Max(0, options.PrefetchCount),
                    },
                    cancellationToken);

                _receivers.TryAdd(receiver, 0);
                await DrainSessionAsync(receiver, onMessage, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                // No session had messages within the accept window — ordinary idle, so loop straight back round.
                continue;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.SessionLockLost)
            {
                // The session lock expired: the messages are unsettled, Service Bus re-offers, and the inbox dedupes.
                LogSessionLockLost(subscription);
            }
            catch (Exception ex)
            {
                LogReceiveFailed(subscription, ex);
                await DelayAsync(FaultBackoff, cancellationToken);
            }
            finally
            {
                // Released only on a clean rotation; on cancellation draining handlers still settle through it.
                if (receiver is not null && !cancellationToken.IsCancellationRequested)
                {
                    _receivers.TryRemove(receiver, out _);
                    await DisposeQuietlyAsync(receiver);
                }
            }
        }
    }

    /// <summary>Pull from a locked session until it runs dry, then hand the loop back so the lock can be released.</summary>
    private async Task DrainSessionAsync(
        ServiceBusSessionReceiver receiver,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await receiver.ReceiveMessagesAsync(
                Math.Max(1, options.MaxMessagesPerReceive),
                options.SessionIdleTimeout,
                cancellationToken);

            if (batch.Count == 0)
                return;

            foreach (var message in batch)
                await DispatchAsync(receiver, message, onMessage, cancellationToken);
        }
    }

    /// <summary>Reconstruct one message and hand it to the pipeline; dead-letter it natively when it cannot be reconstructed.</summary>
    private async ValueTask DispatchAsync(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        var envelope = TryReconstruct(message);
        if (envelope is null)
        {
            // Native DLQ preserves the unparseable message with its reason attached instead of dropping it.
            LogUnparseable(message.MessageId);
            await receiver.DeadLetterMessageAsync(message, "unparseable", "The message carries no resolvable wt-event-type, or its body failed to deserialize", cancellationToken);
            return;
        }

        try
        {
            // The pipeline settles via the context (ctx.Acknowledge on success / ctx.DeadLetter on exhaustion).
            await onMessage(new AzureServiceBusReceiveContext(envelope, receiver, message), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Unsettled and swallowed: the lock expires and the broker redelivers, so one poison cannot stop the loop.
            LogProcessingError(envelope.MessageId, ex);
        }
    }

    private EventEnvelopeModel? TryReconstruct(ServiceBusReceivedMessage message)
    {
        var headers = DecodeHeaders(message.ApplicationProperties);
        if (!headers.TryGetValue(AzureServiceBusHeaderConstants.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        // Declared content type only: reserved header, then native property; guessing JSON misroutes a legacy body.
        var declaredContentType = ReadOptional(headers, AzureServiceBusHeaderConstants.ContentType) ?? message.ContentType;
        var decoded = SerializerFor(declaredContentType).Deserialize(message.Body.ToMemory().Span, eventType);
        if (decoded.IsFailure(out _, out var body))
            return null;

        return new EventEnvelopeModel
        {
            // Native property, then the reserved header; a fresh GUID last, since an unchosen id disables inbox dedupe.
            MessageId = FirstNonEmpty(message.MessageId, ReadOptional(headers, MessageHeaderConstants.MessageId)) ?? Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,

            // Subject is the routing key the send path stamped and a subscription filter matched.
            Destination = message.Subject ?? string.Empty,
            CorrelationId = message.CorrelationId,

            // Native delivery count, incremented by the broker on every lock expiry or abandon; starts at 1.
            DeliveryCount = message.DeliveryCount,
            ContentType = headers.TryGetValue(AzureServiceBusHeaderConstants.ContentType, out var contentType) ? contentType : message.ContentType ?? "application/json",

            // Session id first: the key the broker ordered by. The header is the fallback on a non-session entity.
            PartitionKey = FirstNonEmpty(message.SessionId, message.PartitionKey, ReadOptional(headers, AzureServiceBusHeaderConstants.PartitionKey)),

            // Native properties first; the reserved header is the fallback for a message bridged from another broker.
            ReplyTo = FirstNonEmpty(message.ReplyTo, ReadOptional(headers, MessageHeaderConstants.ReplyTo)),
            ConversationId = ReadOptional(headers, AzureServiceBusHeaderConstants.ConversationId),
            TimeToLive = message.TimeToLive == TimeSpan.MaxValue ? null : message.TimeToLive,
            Headers = headers,
        };
    }

    private static Dictionary<string, string> DecodeHeaders(IReadOnlyDictionary<string, object> properties)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            decoded[key] = value switch
            {
                string text => text,
                byte[] bytes => Encoding.UTF8.GetString(bytes),

                // AMQP carries typed values; the header bag is string-valued, so anything else renders invariantly.
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty,
            };
        }

        return decoded;
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer whenever no registry is wired, which is what keeps a single-serializer container behaving exactly as it did.</summary>
    private IMessageSerializer SerializerFor(string? contentType) => serializerRegistry?.Resolve(contentType) ?? serializer;

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
            if (!string.IsNullOrEmpty(candidate))
                return candidate;

        return null;
    }

    /// <summary>Delay that treats cancellation as a normal wake-up, so a backoff cannot throw out of a loop that is shutting down.</summary>
    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async ValueTask DisposeQuietlyAsync(ServiceBusReceiver receiver)
    {
        try
        {
            await receiver.DisposeAsync();
        }
        catch (Exception ex)
        {
            // Best-effort: a receiver whose link is already gone cannot be closed politely.
            LogReceiverDisposeFailed(ex);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        foreach (var receiver in _receivers.Keys)
        {
            _receivers.TryRemove(receiver, out _);
            await DisposeQuietlyAsync(receiver);
        }

        // The ServiceBusClient belongs to the shared singleton; disposing it here closes the send path's connection.
    }

    [LoggerMessage(EventId = 6701, Level = LogLevel.Warning, Message = "Dead-lettering unparseable Service Bus message {MessageId}")]
    private partial void LogUnparseable(string? messageId);

    [LoggerMessage(EventId = 6702, Level = LogLevel.Error, Message = "Service Bus message {MessageId} processing failed; not settled (the lock will expire and redeliver)")]
    private partial void LogProcessingError(string messageId, Exception exception);

    [LoggerMessage(EventId = 6703, Level = LogLevel.Error, Message = "Receiving from Service Bus subscription {Subscription} failed; retrying after a backoff")]
    private partial void LogReceiveFailed(string subscription, Exception exception);

    [LoggerMessage(EventId = 6704, Level = LogLevel.Information, Message = "Consuming Service Bus subscription {Subscription} (sessions {RequiresSession})")]
    private partial void LogConsumingSubscription(string subscription, bool requiresSession);

    [LoggerMessage(EventId = 6705, Level = LogLevel.Warning, Message = "Service Bus session lock lost on subscription {Subscription}; the session will be re-offered and its unsettled messages redelivered")]
    private partial void LogSessionLockLost(string subscription);

    [LoggerMessage(EventId = 6706, Level = LogLevel.Debug, Message = "Disposing a Service Bus receiver failed; it is already closed")]
    private partial void LogReceiverDisposeFailed(Exception exception);

    [LoggerMessage(EventId = 6707, Level = LogLevel.Warning, Message = "No Service Bus consume endpoints: no IEventHandler<> is registered, so there is nothing to provision or consume")]
    private partial void LogNoConsumeEndpoints();
}
