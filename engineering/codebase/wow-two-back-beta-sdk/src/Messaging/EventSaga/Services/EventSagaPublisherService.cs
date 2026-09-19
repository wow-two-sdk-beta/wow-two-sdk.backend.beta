using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga.Services;

/// <summary>
/// Provides guarded publishing of step-emitted events through the registered <see cref="IEventBus"/>.
/// </summary>
/// <remarks>
///   - declare every step destination with <see cref="EventSagaBuilder.SendsTo{TEvent}"/>
///   - RabbitMQ drops a message sent to an unbound routing key, never refuses it
///   - an undeclared unbound address is logged once, unless <see cref="EventSagaOptions.UnroutableDestination"/> makes it fatal
/// </remarks>
internal sealed partial class EventSagaPublisherService(
    IEventBus bus,
    ILogger<EventSagaPublisherService> logger,
    ITopologyService? topology = null,
    DestinationBindingRegistry? declaredDestinations = null,
    EventSagaOptions? options = null) : IEventSagaPublisherService
{
    private readonly ConcurrentDictionary<string, byte> _warnedDestinations = new(StringComparer.Ordinal);
    private readonly EventSagaOptions _options = options ?? new EventSagaOptions();

    public ValueTask SendAsync<TEvent>(string destination, TEvent @event, EventSagaContext context, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        GuardRoutable(destination, @event, typeof(TEvent));
        return bus.SendAsync(destination, @event, new SendOptions { CorrelationId = context.CorrelationId }, cancellationToken);
    }

    private void GuardRoutable(string destination, object body, Type bodyType)
    {
        // No topology registered means no key-based routing to get wrong.
        if (topology is null || string.IsNullOrEmpty(destination))
            return;

        // A declared destination-and-type pair is accounted for; a second type sent to that address is not covered.
        if (declaredDestinations?.IsDeclared(destination, bodyType) == true)
            return;

        // A destination of separators only sanitizes to an empty key, which binds nothing — unroutable, not an error.
        var key = topology.ResolveRoutingKey(new EventEnvelopeModel { MessageId = string.Empty, Body = body, BodyType = bodyType, Destination = destination });
        if (key.Length != 0 && topology.BindsRoutingKey(key))
            return;

        if (_options.UnroutableDestination == UnroutableDestinationBehavior.Throw)
            throw new InvalidOperationException(
                $"Saga destination '{destination}' resolves to routing key '{key}', which no endpoint in this process binds, "
                + $"and it was not declared. Declare it with EventSagaBuilder.SendsTo<{bodyType.Name}>(\"{destination}\") — or with "
                + $"AddDestinationBinding — so topology binds it. A send to an unbound key is discarded by the broker, not refused.");

        // Once per destination: a saga step in a loop would otherwise log per message.
        if (_warnedDestinations.TryAdd(destination, 0))
            LogUnboundDestination(destination, key);
    }

    [LoggerMessage(EventId = 6104, Level = LogLevel.Warning, Message = "Saga destination {Destination} resolves to routing key {RoutingKey}, which no endpoint in this process binds and no registration declared; declare it via SendsTo<T>() or verify another service binds it, or the message is dropped unrouted")]
    private partial void LogUnboundDestination(string destination, string routingKey);
}
