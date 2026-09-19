using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Services;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

/// <summary>
/// Dispatches application events through the registered delivery transport. Builds the <see cref="EventEnvelopeModel"/> (id, correlation, transport
/// hints), starts a PRODUCER span and injects W3C trace-context into the headers, then hands the envelope to the
/// registered <see cref="ISendTransport"/>. Shared by every transport (in-memory channel, RabbitMQ, …).
/// Registered <see cref="IPublishObservingInterceptor"/>s are notified around the hand-off; they only watch, and cannot change it.
/// </summary>
/// <remarks>
///   - <paramref name="claimCheck"/> alone may change the envelope, putting a pointer on the wire in place of an oversized body
///   - wire one with <c>AddEventClaimCheck()</c>
/// </remarks>
internal sealed class TransportEventBus(
    ISendTransport sendTransport,
    TimeProvider timeProvider,
    IMessagingMetricsService metrics,
    IEnumerable<IPublishObservingInterceptor> publishObservers,
    ILogger<TransportEventBus> logger,
    ClaimCheckOffloader? claimCheck = null) : IEventBus
{
    // Materialized once: the send path only pays a length check when nothing is observing.
    private readonly IPublishObservingInterceptor[] _publishObservers = [.. publishObservers];

    public ValueTask PublishAsync<TEvent>(TEvent @event, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        return EnqueueAsync(@event, typeof(TEvent).Name, options?.MessageId, options?.CorrelationId, options?.ConversationId, options?.CausationId, options?.ReplyTo, options?.Delay, options?.PartitionKey, options?.Durable, options?.Priority, options?.TimeToLive, options?.Headers, cancellationToken);
    }

    public ValueTask SendAsync<TEvent>(string destination, TEvent @event, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(@event);
        return EnqueueAsync(@event, destination, options?.MessageId, options?.CorrelationId, options?.ConversationId, options?.CausationId, options?.ReplyTo, options?.Delay, options?.PartitionKey, options?.Durable, options?.Priority, options?.TimeToLive, options?.Headers, cancellationToken);
    }

    private async ValueTask EnqueueAsync<TEvent>(
        TEvent @event,
        string destination,
        string? messageId,
        string? correlationId,
        string? conversationId,
        string? causationId,
        string? replyTo,
        TimeSpan? delay,
        string? partitionKey,
        bool? durable,
        int? priority,
        TimeSpan? timeToLive,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
        where TEvent : class, IEvent
    {
        var now = timeProvider.GetUtcNow();
        var notBefore = delay is { } d && d > TimeSpan.Zero ? now + d : (DateTimeOffset?)null;
        var id = messageId ?? Guid.NewGuid().ToString("N");

        using var activity = MessagingDiagnosticConstants.Source.StartActivity(destination, ActivityKind.Producer);
        if (activity is not null)
        {
            activity.SetTag("messaging.operation.name", "publish");
            activity.SetTag("messaging.destination.name", destination);
            activity.SetTag("messaging.message.id", id);
        }

        var envelope = new EventEnvelopeModel
        {
            MessageId = id,
            Body = @event,
            BodyType = typeof(TEvent),
            Destination = destination,
            CorrelationId = correlationId,
            ConversationId = conversationId,
            CausationId = causationId,
            ReplyTo = replyTo,
            DeliveryCount = 0,
            NotBeforeUtc = notBefore,
            PartitionKey = partitionKey,
            Durable = durable ?? false,
            Priority = priority,
            TimeToLive = timeToLive,
            Headers = BuildPropagatedHeaders(headers, activity),
        };

        // Runs before the observers and the transport, so both see the envelope that actually travels.
        if (claimCheck is not null)
            envelope = await claimCheck.PrepareAsync(envelope, cancellationToken);

        await _publishObservers.NotifyPrePublishAsync(envelope, logger, cancellationToken);

        try
        {
            await sendTransport.SendAsync(envelope, cancellationToken);
        }
        // With no observer registered the catch is never entered and the fault propagates untouched.
        catch (Exception ex) when (_publishObservers.Length != 0 && !ObservingInterceptorExtensions.IsCancellation(ex, cancellationToken))
        {
            await _publishObservers.NotifyPublishFaultAsync(envelope, ex, logger, cancellationToken);
            throw;
        }

        metrics.RecordPublished(destination, typeof(TEvent)); // after the hand-off, so a failed send is not counted as sent
        await _publishObservers.NotifyPostPublishAsync(envelope, logger, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> BuildPropagatedHeaders(IReadOnlyDictionary<string, string>? headers, Activity? activity)
    {
        var current = activity ?? Activity.Current;
        if (current is null)
            return headers ?? ReadOnlyDictionary<string, string>.Empty;

        var propagated = headers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);

        if (current.Id is { } traceParent)
            propagated[MessagingDiagnosticConstants.TraceParentHeader] = traceParent;
        if (!string.IsNullOrEmpty(current.TraceStateString))
            propagated[MessagingDiagnosticConstants.TraceStateHeader] = current.TraceStateString;

        return propagated;
    }
}
