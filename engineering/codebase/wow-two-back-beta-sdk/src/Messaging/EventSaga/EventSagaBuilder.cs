using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga.Services;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>Fluent builder for a declarative event saga (routing slip). Steps run in the order added.</summary>
/// <remarks>
///   - runs in memory, start to finish — the itinerary does not survive a crash
///   - for a flow persisted between messages, use <see cref="WoW.Two.Sdk.Backend.Beta.Messaging.Saga.SagaStateMachine{TState}"/> instead
/// </remarks>
public sealed class EventSagaBuilder
{
    private readonly string _name;
    private readonly List<Type> _stepTypes = [];
    private readonly List<DestinationBinding> _destinations = [];

    private EventSagaBuilder(string name) => _name = name;

    /// <summary>Begin a named saga definition.</summary>
    /// <param name="name">The saga name.</param>
    public static EventSagaBuilder Named(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new EventSagaBuilder(name);
    }

    /// <summary>Append a step.</summary>
    /// <typeparam name="TStep">A step type implementing <see cref="IEventSagaStep"/>.</typeparam>
    public EventSagaBuilder Step<TStep>()
        where TStep : IEventSagaStep
    {
        _stepTypes.Add(typeof(TStep));
        return this;
    }

    /// <summary>Append a step by type.</summary>
    /// <param name="stepType">A type implementing <see cref="IEventSagaStep"/>.</param>
    public EventSagaBuilder Step(Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);
        if (!typeof(IEventSagaStep).IsAssignableFrom(stepType))
            throw new ArgumentException($"Step type {stepType.FullName} must implement {nameof(IEventSagaStep)}.", nameof(stepType));

        _stepTypes.Add(stepType);
        return this;
    }

    /// <summary>
    /// Declare a logical destination this saga's steps send <typeparamref name="TEvent"/> to, so registration binds
    /// that address instead of leaving it unroutable.
    /// </summary>
    /// <remarks>
    ///   - an undeclared destination resolves to a routing key nothing matches — the broker discards it silently
    ///   - the bound set is otherwise the endpoint queue names and the consumed types' keys
    ///   - declaring a destination another service consumes exempts it from the unbound warning and from <see cref="UnroutableDestinationBehavior.Throw"/>
    /// </remarks>
    /// <typeparam name="TEvent">The event type the step sends to that destination.</typeparam>
    /// <param name="destination">The destination name, exactly as the step passes it to <see cref="IEventSagaPublisherService.SendAsync{TEvent}"/>.</param>
    public EventSagaBuilder SendsTo<TEvent>(string destination)
        where TEvent : class, IEvent
        => SendsTo(destination, typeof(TEvent));

    /// <summary>Declare a logical destination by type.</summary>
    /// <param name="destination">The destination name a step addresses.</param>
    /// <param name="messageType">The event type sent to it; must implement <see cref="IEvent"/>.</param>
    public EventSagaBuilder SendsTo(string destination, Type messageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(messageType);
        if (!typeof(IEvent).IsAssignableFrom(messageType))
            throw new ArgumentException($"Destination type {messageType.FullName} must implement {nameof(IEvent)}.", nameof(messageType));

        _destinations.Add(new DestinationBinding { Destination = destination, MessageType = messageType });
        return this;
    }

    /// <summary>Build the immutable definition.</summary>
    public EventSagaDefinition Build() => new(_name, _stepTypes.AsReadOnly(), _destinations.AsReadOnly());
}
