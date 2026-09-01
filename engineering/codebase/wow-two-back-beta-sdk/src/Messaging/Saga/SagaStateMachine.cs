namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// A declarative, event-driven saga: a set of states, and per state the events it reacts to. Derive from it, declare
/// the behaviour in the constructor with <c>Initially</c> / <c>During</c> / <c>DuringAny</c>, and register it with
/// <c>AddSaga</c>.
/// </summary>
/// <typeparam name="TState">The persisted instance state.</typeparam>
/// <remarks>
///   - Never hold per-message state on the machine
///   - resolve anything a transition needs from <see cref="SagaTransitionContext{TState,TEvent}.Services"/>
///   - for a linear itinerary this process drives, use <see cref="WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga.EventSagaBuilder"/> instead
/// </remarks>
public abstract class SagaStateMachine<TState>
    where TState : class, ISagaState
{
    private readonly Dictionary<Type, SagaEventBinding<TState>> _bindings = [];
    private readonly Dictionary<Type, string> _timeoutNames = [];
    private bool _validated;

    /// <summary>Create the machine.</summary>
    protected SagaStateMachine()
    {
    }

    /// <summary>The machine's name, used in logs. Defaults to the type name.</summary>
    public virtual string Name => GetType().Name;

    /// <summary>Every event type the machine reacts to — the set <c>AddSaga</c> turns into handler registrations and broker bindings.</summary>
    public IReadOnlyCollection<Type> ObservedEventTypes => [.. _bindings.Keys];

    /// <summary>Begin a clause for <typeparamref name="TEvent"/>, correlating it by a key on the event.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="correlateBy">Extracts the instance key from the event; declared once per event type, and a second declaration for that type throws.</param>
    protected static SagaEventClause<TState, TEvent> When<TEvent>(Func<TEvent, string?> correlateBy)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(correlateBy);
        // The cast is safe: the coordinator only invokes a binding's correlator for its own EventType.
        return new SagaEventClause<TState, TEvent>((message, _) => correlateBy((TEvent)message));
    }

    /// <summary>Begin a clause for <typeparamref name="TEvent"/>, reusing the correlation declared elsewhere on the machine.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <remarks>
    ///   - a type scheduled by <c>Schedule</c> correlates by the envelope's correlation id, wired automatically
    ///   - every other type must be correlated on one clause, or registration fails
    /// </remarks>
    protected static SagaEventClause<TState, TEvent> When<TEvent>()
        where TEvent : class, IEvent
        => new(correlate: null);

    /// <summary>Declare what happens to an event that arrives with no instance yet — the clauses that create one.</summary>
    /// <param name="clauses">Clauses bound to <see cref="SagaStateConstants.Initial"/>.</param>
    protected void Initially(params SagaEventClause<TState>[] clauses) => Bind(SagaStateConstants.Initial, clauses);

    /// <summary>Declare what an instance in <paramref name="state"/> does with each event.</summary>
    /// <param name="state">The source state name.</param>
    /// <param name="clauses">Clauses bound to that state.</param>
    protected void During(string state, params SagaEventClause<TState>[] clauses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        Bind(state, clauses);
    }

    /// <summary>
    /// Declare clauses that apply in every state — a cancellation, a correction, an audit event. A state's own clause
    /// for the same event type wins; this is the fallback.
    /// </summary>
    /// <param name="clauses">Clauses bound to every state.</param>
    protected void DuringAny(params SagaEventClause<TState>[] clauses) => Bind(SagaStateConstants.Any, clauses);

    /// <summary>Correlate an event type by the envelope's correlation id rather than a field on the body.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <remarks>
    ///   - use for an event produced inside the same flow — a reply, a fault, a timeout
    ///   - the body carries no key of its own there
    ///   - the correlation id already identifies the instance
    /// </remarks>
    protected void CorrelateByCorrelationId<TEvent>()
        where TEvent : class, IEvent
        => DeclareCorrelation(GetOrAddBinding<TEvent>(), typeof(TEvent), static (_, envelope) => envelope.CorrelationId);

    internal SagaEventBinding<TState>? FindBinding(Type eventType)
        => _bindings.TryGetValue(eventType, out var binding) ? binding : null;

    internal string? TimeoutNameFor(Type eventType)
        => _timeoutNames.TryGetValue(eventType, out var name) ? name : null;

    internal SagaEventBinding<TState, TEvent> GetOrAddBinding<TEvent>()
        where TEvent : class, IEvent
    {
        if (_bindings.TryGetValue(typeof(TEvent), out var existing))
            return (SagaEventBinding<TState, TEvent>)existing;

        var binding = new SagaEventBinding<TState, TEvent>();
        _bindings[typeof(TEvent)] = binding;
        return binding;
    }

    internal void DeclareCorrelation(SagaEventBinding<TState> binding, Type eventType, Func<object, EventEnvelope, string?> correlate)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.Correlate is not null)
            throw new InvalidOperationException($"Saga '{Name}' already declares a correlation for {eventType.Name}. Declare it once — on the first clause for that type — and use the parameterless When<{eventType.Name}>() afterwards.");

        binding.Correlate = correlate;
    }

    internal void DeclareTimeout(Type timeoutType, string name)
    {
        // Last declaration wins: one timeout type is one timeout, whichever transition scheduled it.
        _timeoutNames[timeoutType] = name;
    }

    /// <summary>
    /// Freeze and check the definition: every observed event type must know how to correlate. Called by <c>AddSaga</c>,
    /// at registration, so a machine that could never route an event fails at startup instead of dropping messages.
    /// </summary>
    internal void Validate()
    {
        if (_validated)
            return;

        foreach (var (eventType, binding) in _bindings)
        {
            if (binding.Correlate is not null)
                continue;

            // A type the machine schedules itself carries the saga's own correlation id on the envelope.
            if (_timeoutNames.ContainsKey(eventType))
            {
                binding.Correlate = static (_, envelope) => envelope.CorrelationId;
                continue;
            }

            throw new InvalidOperationException($"Saga '{Name}' observes {eventType.Name} but never declares how to correlate it. Use When<{eventType.Name}>(e => e.Key), or CorrelateByCorrelationId<{eventType.Name}>().");
        }

        _validated = true;
    }

    private void Bind(string sourceState, SagaEventClause<TState>[] clauses)
    {
        ArgumentNullException.ThrowIfNull(clauses);
        foreach (var clause in clauses)
        {
            ArgumentNullException.ThrowIfNull(clause);
            clause.Attach(this, sourceState);
        }
    }
}
