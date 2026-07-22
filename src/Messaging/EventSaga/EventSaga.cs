using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>
/// One step in a declarative event saga (routing slip). Steps run in order; each may write its result into the
/// shared <see cref="EventSagaContext"/> (the "result passed along"). On a downstream failure the runner walks the
/// completed steps in reverse calling <see cref="CompensateAsync"/> — automatic rollback.
/// </summary>
public interface IEventSagaStep
{
    /// <summary>Display name (used in logs and the Mermaid diagram).</summary>
    string Name { get; }

    /// <summary>Run the step. Return <see cref="EventSagaStepOutcome.Faulted"/> (or throw) to trigger compensation.</summary>
    /// <param name="context">The shared saga context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<EventSagaStepOutcome> ExecuteAsync(EventSagaContext context, CancellationToken cancellationToken);

    /// <summary>Undo this step's effect. Invoked in reverse order when a later step fails. No-op by default.</summary>
    /// <param name="context">The shared saga context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask CompensateAsync(EventSagaContext context, CancellationToken cancellationToken);
}

/// <summary>Convenience base for an <see cref="IEventSagaStep"/> — names itself after its type and compensates as a no-op.</summary>
public abstract class EventSagaStep : IEventSagaStep
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public abstract ValueTask<EventSagaStepOutcome> ExecuteAsync(EventSagaContext context, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask CompensateAsync(EventSagaContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

/// <summary>The result of running a single saga step.</summary>
public sealed record EventSagaStepOutcome
{
    private EventSagaStepOutcome(bool succeeded, string? failureReason)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
    }

    /// <summary>Whether the step succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>Failure reason when <see cref="Succeeded"/> is false.</summary>
    public string? FailureReason { get; }

    /// <summary>A successful outcome — the runner advances to the next step.</summary>
    public static EventSagaStepOutcome Completed() => new(true, null);

    /// <summary>A failed outcome — the runner compensates completed steps in reverse.</summary>
    /// <param name="reason">Why the step failed.</param>
    public static EventSagaStepOutcome Faulted(string reason) => new(false, reason);
}

/// <summary>Shared, correlated state threaded through every step of an event-saga run.</summary>
public sealed class EventSagaContext
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

    /// <summary>Create a context with an optional seed value and a fresh correlation id.</summary>
    /// <param name="seed">Optional initial input for the first step.</param>
    public EventSagaContext(object? seed = null)
    {
        Seed = seed;
        CorrelationId = Guid.NewGuid().ToString("N");
    }

    /// <summary>Correlation id shared by every event the saga emits.</summary>
    public string CorrelationId { get; }

    /// <summary>The seed value supplied at construction.</summary>
    public object? Seed { get; }

    /// <summary>The accumulated step outputs.</summary>
    public IReadOnlyDictionary<string, object?> Items => _items;

    /// <summary>Store a value for downstream steps.</summary>
    /// <param name="key">Item key.</param>
    /// <param name="value">Item value.</param>
    public void Set(string key, object? value) => _items[key] = value;

    /// <summary>Read a typed value written by an earlier step, or <c>default</c> if absent / mistyped.</summary>
    /// <typeparam name="T">Expected value type.</typeparam>
    /// <param name="key">Item key.</param>
    public T? Get<T>(string key) => _items.TryGetValue(key, out var value) && value is T typed ? typed : default;
}

/// <summary>The outcome of a whole event-saga run.</summary>
/// <param name="Succeeded">Whether every step completed.</param>
/// <param name="FailedStep">Name of the step that failed, if any.</param>
/// <param name="FailureReason">Why the saga failed, if it did.</param>
/// <param name="CompensatedSteps">Names of steps compensated (reverse order), if the saga rolled back.</param>
public sealed record EventSagaResult(
    bool Succeeded,
    string? FailedStep,
    string? FailureReason,
    IReadOnlyList<string> CompensatedSteps);

/// <summary>An immutable, ordered event-saga definition (the itinerary as data).</summary>
public sealed class EventSagaDefinition
{
    internal EventSagaDefinition(string name, IReadOnlyList<Type> stepTypes, IReadOnlyList<DestinationBinding> destinations)
    {
        Name = name;
        StepTypes = stepTypes;
        Destinations = destinations;
    }

    /// <summary>The saga name.</summary>
    public string Name { get; }

    /// <summary>The ordered step types.</summary>
    public IReadOnlyList<Type> StepTypes { get; }

    /// <summary>
    /// The logical destinations this saga's steps address, declared via <see cref="EventSagaBuilder.SendsTo{TEvent}"/>.
    /// Registration binds each one whose type this process consumes, which is what keeps a step's send from being
    /// dropped as unroutable; a destination another service consumes is recorded but not bound.
    /// </summary>
    public IReadOnlyList<DestinationBinding> Destinations { get; }

    /// <summary>Render the itinerary as a Mermaid <c>flowchart</c> for documentation / visualization.</summary>
    public string ToMermaid()
    {
        var builder = new StringBuilder();
        builder.AppendLine("flowchart LR");
        builder.Append("  start([start])");

        var previous = "start";
        for (var i = 0; i < StepTypes.Count; i++)
        {
            var id = $"s{i}";
            builder.AppendLine();
            builder.Append(CultureInfo.InvariantCulture, $"  {previous} --> {id}[{StepTypes[i].Name}]");
            previous = id;
        }

        return builder.ToString();
    }
}

/// <summary>Fluent builder for a declarative event saga (routing slip). Steps run in the order added.</summary>
/// <remarks>
/// An itinerary this process drives start to finish, in memory: it does not survive a crash, and it does not wait for
/// the world. For a flow that lives between messages — correlated by a business key, persisted, woken by timeouts —
/// use <see cref="WoW.Two.Sdk.Backend.Beta.Messaging.Saga.SagaStateMachine{TState}"/> instead. Both are supported; they
/// answer different questions.
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
    /// <para>
    /// A step addresses its output by name — <c>transport.SendAsync("order-fulfilment", evt, context, ct)</c> — and
    /// since topology replaced RabbitMQ's catch-all binding, only the endpoint queue names and the consumed types'
    /// keys are bound. An undeclared name therefore resolves to a routing key nothing matches, and the broker
    /// discards the message without refusing it, because a publish is <c>mandatory: false</c>.
    /// </para>
    /// <para>
    /// The address is bound only where <typeparamref name="TEvent"/> is consumed in this process, so declaring a
    /// destination another service owns is both safe and useful: it tells the transport the address is accounted
    /// for, which is what separates a legitimate cross-service send from a misspelt local one.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEvent">The event type the step sends to that destination.</typeparam>
    /// <param name="destination">The destination name, exactly as the step passes it to <see cref="IEventSagaTransport.SendAsync{TEvent}"/>.</param>
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

/// <summary>What the default <see cref="IEventSagaTransport"/> does when a step addresses a destination this process does not bind.</summary>
public enum UnroutableDestinationBehavior
{
    /// <summary>
    /// Log once per destination and send anyway. The default, because an address consumed by <em>another</em> service
    /// is unbound here and perfectly legitimate — this process cannot see a remote binding.
    /// </summary>
    Warn = 0,

    /// <summary>
    /// Throw <see cref="InvalidOperationException"/> instead of sending. For a service whose saga destinations are all
    /// local: every one is then declarable via <see cref="EventSagaBuilder.SendsTo{TEvent}"/>, so anything unbound is a
    /// configuration error and a silent drop is never the right outcome.
    /// </summary>
    Throw = 1,
}

/// <summary>Options for declarative (routing-slip) event sagas.</summary>
public sealed class EventSagaOptions
{
    /// <summary>
    /// How a send to an unbound destination is handled. Default <see cref="UnroutableDestinationBehavior.Warn"/>.
    /// A destination declared through <see cref="EventSagaBuilder.SendsTo{TEvent}"/> whose type this process does not
    /// consume is exempt either way — it is a known cross-service address, not an accident.
    /// </summary>
    public UnroutableDestinationBehavior UnroutableDestination { get; set; } = UnroutableDestinationBehavior.Warn;
}

/// <summary>Executes an <see cref="EventSagaDefinition"/>, compensating completed steps in reverse on failure.</summary>
public interface IEventSagaRunner
{
    /// <summary>Run a saga to completion or compensation.</summary>
    /// <param name="definition">The saga to run.</param>
    /// <param name="context">The shared context (carries the seed + accumulated results).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<EventSagaResult> RunAsync(EventSagaDefinition definition, EventSagaContext context, CancellationToken cancellationToken = default);
}

/// <summary>Transport seam letting a saga step emit an event to a destination — in-process by default, broker via an adapter.</summary>
public interface IEventSagaTransport
{
    /// <summary>Send an event to a destination, carrying the saga's correlation id.</summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="destination">
    /// Destination (queue) name. On a key-routed transport this must be a <b>bound</b> address, because an unbound key
    /// is dropped rather than refused. Declare it with <see cref="EventSagaBuilder.SendsTo{TEvent}"/> and registration
    /// binds it; the bound set is otherwise only the endpoint queue names and the consumed types' keys. The default
    /// transport checks the address against the local topology and applies
    /// <see cref="EventSagaOptions.UnroutableDestination"/> to one it cannot account for.
    /// </param>
    /// <param name="event">The event.</param>
    /// <param name="context">The saga context (for correlation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SendAsync<TEvent>(string destination, TEvent @event, EventSagaContext context, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}
