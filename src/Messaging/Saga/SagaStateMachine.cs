namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// A declarative, event-driven saga: a set of states, and per state the events it reacts to. Derive from it, declare
/// the behaviour in the constructor with <c>Initially</c> / <c>During</c> / <c>DuringAny</c>, and register it with
/// <c>AddSaga</c>.
/// </summary>
/// <typeparam name="TState">The persisted instance state.</typeparam>
/// <remarks>
/// <para>
/// This is the long-running sibling of the routing-slip <see cref="WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga.EventSagaBuilder"/>,
/// not a replacement for it. A routing slip is one process running an ordered itinerary with compensation; a state machine is state that
/// lives in a repository between messages, reacting to events that arrive minutes or days apart, from anywhere.
/// Both stay: pick the itinerary for a linear sequence you drive, the state machine for a flow the world drives.
/// </para>
/// <para>
/// The machine is a <b>definition</b> — built once, registered as a singleton, and free of per-message state. Anything
/// a transition needs is resolved from <see cref="SagaTransitionContext{TState,TEvent}.Services"/>, the scope of the
/// message being handled.
/// </para>
/// <example>
/// <code>
/// public sealed class OrderStateMachine : SagaStateMachine&lt;OrderSagaState&gt;
/// {
///     public const string AwaitingPayment = "awaiting-payment";
///
///     public OrderStateMachine()
///     {
///         Initially(
///             When&lt;OrderPlaced&gt;(e =&gt; e.OrderId)                       // correlation, declared once per event type
///                 .Then(ctx =&gt; ctx.Saga.Total = ctx.Message.Total)
///                 .Schedule(TimeSpan.FromMinutes(30), ctx =&gt; new PaymentOverdue(ctx.CorrelationId))
///                 .TransitionTo(AwaitingPayment));
///
///         During(AwaitingPayment,
///             When&lt;PaymentReceived&gt;(e =&gt; e.OrderId)
///                 .Unschedule&lt;PaymentOverdue&gt;()
///                 .Publish(ctx =&gt; new OrderConfirmed(ctx.CorrelationId))
///                 .Finalize(),
///             When&lt;PaymentOverdue&gt;()                                    // timeouts correlate by envelope id
///                 .Publish(ctx =&gt; new OrderCancelled(ctx.CorrelationId))
///                 .Finalize());
///     }
/// }
/// </code>
/// </example>
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
    /// <param name="correlateBy">
    /// Extracts the instance key from the event. Declared <b>once per event type</b> across the whole machine —
    /// correlation has to run before the instance (and so its state) is known, which makes it a property of the event
    /// type, not of a transition. Declaring it twice for one type throws; use the bare
    /// <see cref="When{TEvent}()"/> on the machine's later clauses for that type.
    /// </param>
    protected static SagaEventClause<TState, TEvent> When<TEvent>(Func<TEvent, string?> correlateBy)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(correlateBy);
        // The binding stores correlators untyped so one dictionary can hold every event type; the cast is safe because
        // the coordinator only invokes a binding's correlator for its own EventType.
        return new SagaEventClause<TState, TEvent>((message, _) => correlateBy((TEvent)message));
    }

    /// <summary>Begin a clause for <typeparamref name="TEvent"/>, reusing the correlation declared elsewhere on the machine.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <remarks>
    /// A type scheduled by <c>Schedule</c> needs nothing further — a timeout the saga sent itself correlates by the
    /// envelope's correlation id, which is wired automatically. Any other type must be correlated by one clause, or
    /// registration fails.
    /// </remarks>
    protected static SagaEventClause<TState, TEvent> When<TEvent>()
        where TEvent : class, IEvent
        => new(correlate: null);

    /// <summary>Declare what happens to an event that arrives with no instance yet — the clauses that create one.</summary>
    /// <param name="clauses">Clauses bound to <see cref="SagaStates.Initial"/>.</param>
    protected void Initially(params SagaEventClause<TState>[] clauses) => Bind(SagaStates.Initial, clauses);

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
    protected void DuringAny(params SagaEventClause<TState>[] clauses) => Bind(SagaStates.Any, clauses);

    /// <summary>Correlate an event type by the envelope's correlation id rather than a field on the body.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <remarks>
    /// The right choice for an event produced inside the same flow (a reply, a fault, a timeout), where the correlation
    /// id already identifies the instance and the body carries no key of its own.
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
        // Last declaration wins on purpose: the same timeout type scheduled from two transitions is one timeout with
        // one name, and a conflicting name is a naming mistake the validation below cannot see either way.
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

            // A type the machine schedules itself correlates by the envelope: the saga stamped its own correlation id
            // on the timeout when it sent it.
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

/// <summary>One event's behaviour in one state. Built by <c>When</c> and attached by <c>Initially</c> / <c>During</c> / <c>DuringAny</c>.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public abstract class SagaEventClause<TState>
    where TState : class, ISagaState
{
    private protected SagaEventClause()
    {
    }

    /// <summary>The event type this clause reacts to.</summary>
    public abstract Type EventType { get; }

    internal abstract void Attach(SagaStateMachine<TState> machine, string sourceState);
}

/// <summary>A typed event clause — the fluent surface: correlate, act, publish, schedule, transition.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
public sealed class SagaEventClause<TState, TEvent> : SagaEventClause<TState>
    where TState : class, ISagaState
    where TEvent : class, IEvent
{
    private readonly List<Func<SagaTransitionContext<TState, TEvent>, CancellationToken, ValueTask>> _activities = [];
    private readonly List<(Type TimeoutType, string Name)> _declaredTimeouts = [];
    private Func<object, EventEnvelope, string?>? _correlate;
    private string? _targetState;
    private bool _finalizes;
    private SagaMissingInstance _missingInstance = SagaMissingInstance.Ignore;

    internal SagaEventClause(Func<object, EventEnvelope, string?>? correlate) => _correlate = correlate;

    /// <inheritdoc />
    public override Type EventType => typeof(TEvent);

    /// <summary>Correlate this event type by a key on the body. Equivalent to the <c>When</c> overload that takes a selector; declared once per event type.</summary>
    /// <param name="correlateBy">Extracts the instance key from the event.</param>
    public SagaEventClause<TState, TEvent> CorrelateBy(Func<TEvent, string?> correlateBy)
    {
        ArgumentNullException.ThrowIfNull(correlateBy);
        _correlate = (message, _) => correlateBy((TEvent)message);
        return this;
    }

    /// <summary>Correlate this event type by the envelope's correlation id instead of a field on the body.</summary>
    public SagaEventClause<TState, TEvent> CorrelateByCorrelationId()
    {
        _correlate = static (_, envelope) => envelope.CorrelationId;
        return this;
    }

    /// <summary>Run an asynchronous activity. Activities run in declaration order, before the state change is written.</summary>
    /// <param name="activity">The activity.</param>
    public SagaEventClause<TState, TEvent> Then(Func<SagaTransitionContext<TState, TEvent>, CancellationToken, ValueTask> activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        _activities.Add(activity);
        return this;
    }

    /// <summary>Run a synchronous activity — the common case of copying event fields onto the instance.</summary>
    /// <param name="activity">The activity.</param>
    public SagaEventClause<TState, TEvent> Then(Action<SagaTransitionContext<TState, TEvent>> activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        _activities.Add((context, _) =>
        {
            activity(context);
            return ValueTask.CompletedTask;
        });

        return this;
    }

    /// <summary>Publish an event built from the transition. Correlation and partition key are stamped from the instance.</summary>
    /// <typeparam name="TOut">The outgoing event type.</typeparam>
    /// <param name="factory">Builds the outgoing event.</param>
    public SagaEventClause<TState, TEvent> Publish<TOut>(Func<SagaTransitionContext<TState, TEvent>, TOut> factory)
        where TOut : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(factory);
        _activities.Add((context, cancellationToken) => context.PublishAsync(factory(context), cancellationToken));
        return this;
    }

    /// <summary>
    /// Schedule a timeout the saga sends to itself, delivered after <paramref name="delay"/> as an ordinary event —
    /// react to it with a <c>When&lt;TTimeout&gt;()</c> clause.
    /// </summary>
    /// <typeparam name="TTimeout">The timeout event type.</typeparam>
    /// <param name="delay">How long from now the timeout fires.</param>
    /// <param name="factory">Builds the timeout event.</param>
    /// <param name="name">Timeout name, used to cancel it. Defaults to the timeout type's name.</param>
    /// <remarks>
    /// The timeout is sent only after the transition's state has been written, so a lost concurrency race cannot leave
    /// a timeout in flight for a transition that never happened.
    /// </remarks>
    public SagaEventClause<TState, TEvent> Schedule<TTimeout>(TimeSpan delay, Func<SagaTransitionContext<TState, TEvent>, TTimeout> factory, string? name = null)
        where TTimeout : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(factory);

        var timeoutName = name ?? typeof(TTimeout).Name;
        _declaredTimeouts.Add((typeof(TTimeout), timeoutName));
        _activities.Add((context, _) =>
        {
            context.ScheduleTimeout(factory(context), delay, timeoutName);
            return ValueTask.CompletedTask;
        });

        return this;
    }

    /// <summary>
    /// Cancel a previously scheduled timeout. The message itself cannot be recalled from a transport that already
    /// accepted it — the instance forgets its token, and the timeout is dropped when it arrives.
    /// </summary>
    /// <typeparam name="TTimeout">The timeout event type.</typeparam>
    /// <param name="name">Timeout name. Defaults to the timeout type's name.</param>
    public SagaEventClause<TState, TEvent> Unschedule<TTimeout>(string? name = null)
        where TTimeout : class, IEvent
    {
        var timeoutName = name ?? typeof(TTimeout).Name;
        _activities.Add((context, _) =>
        {
            context.CancelTimeout(timeoutName);
            return ValueTask.CompletedTask;
        });

        return this;
    }

    /// <summary>Move the instance to another state once the activities have run.</summary>
    /// <param name="state">The target state name.</param>
    public SagaEventClause<TState, TEvent> TransitionTo(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        _targetState = state;
        return this;
    }

    /// <summary>What to do when this event arrives and no instance exists. Applies to the event type as a whole; the strictest declaration wins.</summary>
    /// <param name="policy">The missing-instance policy.</param>
    public SagaEventClause<TState, TEvent> IfMissing(SagaMissingInstance policy)
    {
        _missingInstance = policy;
        return this;
    }

    internal void MarkFinalizing() => _finalizes = true;

    internal override void Attach(SagaStateMachine<TState> machine, string sourceState)
    {
        ArgumentNullException.ThrowIfNull(machine);

        var binding = machine.GetOrAddBinding<TEvent>();
        if (_correlate is not null)
            machine.DeclareCorrelation(binding, typeof(TEvent), _correlate);

        if (!binding.Transitions.TryAdd(sourceState, new SagaTransition<TState, TEvent>
        {
            Activities = _activities.AsReadOnly(),
            TargetState = _targetState,
            Finalizes = _finalizes,
        }))
        {
            throw new InvalidOperationException($"Saga '{machine.Name}' already declares a clause for {typeof(TEvent).Name} in state '{sourceState}'.");
        }

        // Fault outranks Ignore: one clause insisting a missing instance is an error settles it for the type, whatever
        // order the clauses were declared in.
        if (_missingInstance == SagaMissingInstance.Fault)
            binding.MissingInstance = SagaMissingInstance.Fault;

        foreach (var (timeoutType, name) in _declaredTimeouts)
            machine.DeclareTimeout(timeoutType, name);
    }
}

/// <summary>Everything one transition can see and do: the instance, the message, the scope, and the outgoing edges.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
public sealed class SagaTransitionContext<TState, TEvent>
    where TState : class, ISagaState
    where TEvent : class, IEvent
{
    private readonly List<SagaTimeoutRequest> _pendingTimeouts = [];

    internal SagaTransitionContext(TState saga, EventContext<TEvent> @event, IServiceProvider services)
    {
        Saga = saga;
        Event = @event;
        Services = services;
    }

    /// <summary>The saga instance. Mutate it — the coordinator writes it once the activities have run.</summary>
    public TState Saga { get; }

    /// <summary>The event being handled.</summary>
    public TEvent Message => Event.Event;

    /// <summary>The full event context — envelope, headers, and the correlation-propagating publish/send helpers.</summary>
    public EventContext<TEvent> Event { get; }

    /// <summary>The message's DI scope. Resolve application services from here; the machine itself holds none.</summary>
    public IServiceProvider Services { get; }

    /// <summary>The instance's correlation id.</summary>
    public string CorrelationId => Saga.CorrelationId;

    internal IReadOnlyList<SagaTimeoutRequest> PendingTimeouts => _pendingTimeouts;

    internal string? TargetStateOverride { get; private set; }

    internal bool Finalized { get; private set; }

    /// <summary>
    /// Publish an event on behalf of the instance. The saga's correlation id goes on as both the correlation and the
    /// partition key, so everything about one instance stays on one ordering key.
    /// </summary>
    /// <typeparam name="TOut">The outgoing event type.</typeparam>
    /// <param name="event">The outgoing event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask PublishAsync<TOut>(TOut @event, CancellationToken cancellationToken = default)
        where TOut : class, IEvent
        => Event.PublishAsync(@event, new PublishOptions { CorrelationId = CorrelationId, PartitionKey = CorrelationId }, cancellationToken);

    /// <summary>
    /// Override the clause's declared target state — the branch case, where the next state depends on the payload.
    /// Applied after the activities, so the last call wins over <c>TransitionTo</c>.
    /// </summary>
    /// <param name="state">The target state name.</param>
    public void TransitionTo(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        TargetStateOverride = state;
    }

    /// <summary>Schedule a timeout to this instance. Sent after the state is written; cancel it with <see cref="CancelTimeout"/>.</summary>
    /// <typeparam name="TTimeout">The timeout event type.</typeparam>
    /// <param name="timeout">The timeout event.</param>
    /// <param name="delay">How long from now it fires.</param>
    /// <param name="name">Timeout name, used to cancel it. Defaults to the timeout type's name.</param>
    public void ScheduleTimeout<TTimeout>(TTimeout timeout, TimeSpan delay, string? name = null)
        where TTimeout : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(timeout);

        var timeoutName = name ?? typeof(TTimeout).Name;
        var token = Guid.NewGuid().ToString("N");

        // Written onto the instance before the send, and persisted with it: the token on the wire is only live while
        // the instance still holds its twin, which is what makes a cancelled or superseded timeout droppable.
        Saga.TimeoutTokens[timeoutName] = token;

        _pendingTimeouts.Add(new SagaTimeoutRequest
        {
            CorrelationId = CorrelationId,
            Name = timeoutName,
            Token = token,
            Delay = delay,
            Message = timeout,
            MessageType = typeof(TTimeout),
            Publish = (bus, options, cancellationToken) => bus.PublishAsync(timeout, options, cancellationToken),
        });
    }

    /// <summary>Forget a scheduled timeout's token, so the timeout is dropped if it still arrives.</summary>
    /// <param name="name">The timeout name.</param>
    public void CancelTimeout(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Saga.TimeoutTokens.Remove(name);
        _pendingTimeouts.RemoveAll(pending => string.Equals(pending.Name, name, StringComparison.Ordinal));
    }

    internal void MarkFinalized() => Finalized = true;
}

/// <summary>
/// Terminal moves. Both are extension methods because <c>Finalize</c> is a member of <see cref="object"/>: declaring
/// an instance method of that name would hide it.
/// </summary>
public static class SagaFinalizationExtensions
{
    /// <summary>Move the instance to <see cref="SagaStates.Final"/> when this clause runs — the saga is done.</summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="clause">The clause.</param>
    public static SagaEventClause<TState, TEvent> Finalize<TState, TEvent>(this SagaEventClause<TState, TEvent> clause)
        where TState : class, ISagaState
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(clause);
        clause.MarkFinalizing();
        return clause;
    }

    /// <summary>Finalize the instance from inside an activity — the conditional counterpart of the clause-level call.</summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="context">The transition context.</param>
    public static void Finalize<TState, TEvent>(this SagaTransitionContext<TState, TEvent> context)
        where TState : class, ISagaState
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        context.MarkFinalized();
    }
}

/// <summary>One state's reaction to one event type: the activities to run and the edge to take.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
internal sealed class SagaTransition<TState, TEvent>
    where TState : class, ISagaState
    where TEvent : class, IEvent
{
    public required IReadOnlyList<Func<SagaTransitionContext<TState, TEvent>, CancellationToken, ValueTask>> Activities { get; init; }

    public string? TargetState { get; init; }

    public bool Finalizes { get; init; }
}

/// <summary>Type-erased binding for one observed event type — how to correlate it, and what it does per state.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal abstract class SagaEventBinding<TState>
    where TState : class, ISagaState
{
    public Func<object, EventEnvelope, string?>? Correlate { get; set; }

    public SagaMissingInstance MissingInstance { get; set; } = SagaMissingInstance.Ignore;

    public abstract Type EventType { get; }
}

/// <summary>Typed binding — keyed by source state, with <see cref="SagaStates.Any"/> as the fallback key.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
internal sealed class SagaEventBinding<TState, TEvent> : SagaEventBinding<TState>
    where TState : class, ISagaState
    where TEvent : class, IEvent
{
    public override Type EventType => typeof(TEvent);

    public Dictionary<string, SagaTransition<TState, TEvent>> Transitions { get; } = new(StringComparer.Ordinal);
}

/// <summary>A timeout a transition asked for, held until the instance's new state is safely written.</summary>
internal sealed class SagaTimeoutRequest
{
    public required string CorrelationId { get; init; }

    public required string Name { get; init; }

    public required string Token { get; init; }

    public required TimeSpan Delay { get; init; }

    public required object Message { get; init; }

    public required Type MessageType { get; init; }

    /// <summary>Publishes the timeout with its own static type — captured at declaration, where the type is still known.</summary>
    public required Func<IEventBus, PublishOptions, CancellationToken, ValueTask> Publish { get; init; }
}
