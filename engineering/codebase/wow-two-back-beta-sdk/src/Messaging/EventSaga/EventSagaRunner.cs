using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>
/// Default event-saga runner — resolves each step from a fresh DI scope, runs steps in order, and on failure
/// (faulted outcome or thrown exception) compensates the completed steps in reverse. The compensation stack
/// is the Saga Execution Coordinator.
/// </summary>
public sealed partial class EventSagaRunner(IServiceScopeFactory scopeFactory, ILogger<EventSagaRunner> logger) : IEventSagaRunner
{
    /// <inheritdoc />
    public async ValueTask<EventSagaResult> RunAsync(EventSagaDefinition definition, EventSagaContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var completed = new Stack<IEventSagaStep>();

        foreach (var stepType in definition.StepTypes)
        {
            var step = (IEventSagaStep)services.GetRequiredService(stepType);
            EventSagaStepOutcome outcome;
            try
            {
                outcome = await step.ExecuteAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogStepThrew(ex, definition.Name, step.Name);
                outcome = EventSagaStepOutcome.Faulted(ex.Message);
            }

            if (outcome.Succeeded)
            {
                completed.Push(step);
                continue;
            }

            LogStepFailing(definition.Name, step.Name, outcome.FailureReason);
            var compensated = await CompensateAsync(definition, completed, context, cancellationToken);
            return new EventSagaResult(false, step.Name, outcome.FailureReason, compensated);
        }

        return new EventSagaResult(true, null, null, Array.Empty<string>());
    }

    private async ValueTask<IReadOnlyList<string>> CompensateAsync(EventSagaDefinition definition, Stack<IEventSagaStep> completed, EventSagaContext context, CancellationToken cancellationToken)
    {
        var compensated = new List<string>();
        while (completed.Count > 0)
        {
            var step = completed.Pop();
            try
            {
                await step.CompensateAsync(context, cancellationToken);
                compensated.Add(step.Name);
            }
            catch (Exception ex)
            {
                LogCompensationFailed(ex, definition.Name, step.Name);
            }
        }

        return compensated;
    }

    [LoggerMessage(EventId = 6101, Level = LogLevel.Error, Message = "Saga {Saga} step {Step} threw")]
    private partial void LogStepThrew(Exception exception, string saga, string step);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Warning, Message = "Saga {Saga} failing at step {Step}: {Reason}")]
    private partial void LogStepFailing(string saga, string step, string? reason);

    [LoggerMessage(EventId = 6103, Level = LogLevel.Error, Message = "Saga {Saga} compensation for step {Step} failed")]
    private partial void LogCompensationFailed(Exception exception, string saga, string step);
}

/// <summary>
/// Default <see cref="IEventSagaTransport"/> — routes step-emitted events through the registered
/// <see cref="IEventBus"/>, which is the in-memory channel unless a broker adapter is wired.
/// </summary>
/// <remarks>
/// <para>
/// <b>Addressing, since topology (B4).</b> <see cref="IEventBus.SendAsync{TEvent}"/> addresses a message by its
/// <c>destination</c>, and on RabbitMQ that string is now the routing key: the <c>#</c> catch-all binding
/// is gone, and a publish is <c>mandatory: false</c>, so a destination nothing bound is accepted by the broker and
/// dropped. The bound keys are the endpoint queue names and the consumed types' keys — <b>not</b> arbitrary logical
/// names. The in-memory transport is unaffected (it dispatches by body type and only records the destination), and
/// Kafka/NATS/Redis send everything to one topic/subject/stream until <c>RouteByDestination</c> is turned on.
/// </para>
/// <para>
/// <b>The fix is to bind the address, not to report the drop.</b> A step's destinations are declared on the definition
/// (<see cref="EventSagaBuilder.SendsTo{TEvent}"/>) and registration turns each into a
/// <see cref="DestinationBinding"/>, so topology binds it exactly like a type key. That reaches every key-routed
/// broker at once: RabbitMQ binds it on the queue, and Kafka/NATS/Redis derive their subscription set from the same
/// <see cref="EndpointTopology.RoutingKeys"/>.
/// </para>
/// <para>
/// The check below is what remains for an address that was never declared. It cannot simply throw: a destination
/// consumed by <em>another</em> service is unbound here and entirely legitimate, and this process cannot see a remote
/// binding. So an undeclared unbound address is logged once by default and can be made fatal per deployment via
/// <see cref="EventSagaOptions.UnroutableDestination"/>; a <em>declared</em> address is never warned about, because
/// its owner is known. State-machine sagas avoid the question by construction: they publish by type, which always
/// lands on a bound key.
/// </para>
/// </remarks>
internal sealed partial class InProcessEventSagaTransport(
    IEventBus bus,
    ILogger<InProcessEventSagaTransport> logger,
    ITopologyProvider? topology = null,
    DestinationBindingRegistry? declaredDestinations = null,
    IOptions<EventSagaOptions>? options = null) : IEventSagaTransport
{
    private readonly ConcurrentDictionary<string, byte> _warnedDestinations = new(StringComparer.Ordinal);
    private readonly EventSagaOptions _options = options?.Value ?? new EventSagaOptions();

    public ValueTask SendAsync<TEvent>(string destination, TEvent @event, EventSagaContext context, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        GuardRoutable(destination, @event, typeof(TEvent));
        return bus.SendAsync(destination, @event, new SendOptions { CorrelationId = context.CorrelationId }, cancellationToken);
    }

    private void GuardRoutable(string destination, object body, Type bodyType)
    {
        // No topology registered means no key-based routing to get wrong (in-memory, and Kafka/NATS/Redis with
        // RouteByDestination off).
        if (topology is null || string.IsNullOrEmpty(destination))
            return;

        // Declared means this destination-and-type pair is accounted for: bound here when this process consumes the
        // type, deliberately unbound when another service does. Either way it is not an accident, so it is not
        // reported. The type is part of the check — a second type sent to a declared address is not covered by it.
        if (declaredDestinations?.IsDeclared(destination, bodyType) == true)
            return;

        // Resolved exactly the way the send transport will resolve it, rather than re-deriving the rule here. A
        // destination made only of separators sanitizes to an empty key, which binds nothing — unroutable, not an
        // argument error, so it takes the same path as any other unbound address.
        var key = topology.ResolveRoutingKey(new EventEnvelope { MessageId = string.Empty, Body = body, BodyType = bodyType, Destination = destination });
        if (key.Length != 0 && topology.BindsRoutingKey(key))
            return;

        if (_options.UnroutableDestination == UnroutableDestinationBehavior.Throw)
            throw new InvalidOperationException(
                $"Saga destination '{destination}' resolves to routing key '{key}', which no endpoint in this process binds, "
                + $"and it was not declared. Declare it with EventSagaBuilder.SendsTo<{bodyType.Name}>(\"{destination}\") — or with "
                + $"AddDestinationBinding — so topology binds it. A send to an unbound key is discarded by the broker, not refused.");

        // Once per destination: a saga step in a loop would otherwise log per message. An undeclared address that
        // another process consumes lands here too — hence "declare or verify", not "broken".
        if (_warnedDestinations.TryAdd(destination, 0))
            LogUnboundDestination(destination, key);
    }

    [LoggerMessage(EventId = 6104, Level = LogLevel.Warning, Message = "Saga destination {Destination} resolves to routing key {RoutingKey}, which no endpoint in this process binds and no registration declared; declare it via SendsTo<T>() or verify another service binds it, or the message is dropped unrouted")]
    private partial void LogUnboundDestination(string destination, string routingKey);
}
