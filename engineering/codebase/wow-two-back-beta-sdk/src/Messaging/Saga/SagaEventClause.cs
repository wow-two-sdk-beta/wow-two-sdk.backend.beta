using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

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
    private Func<object, EventEnvelopeModel, string?>? _correlate;
    private string? _targetState;
    private bool _finalizes;
    private SagaMissingInstance _missingInstance = SagaMissingInstance.Ignore;

    internal SagaEventClause(Func<object, EventEnvelopeModel, string?>? correlate) => _correlate = correlate;

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
    ///   - sent only after the transition's state is written — a lost concurrency race leaves no timeout in flight
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

        // Fault outranks Ignore for the whole type, whatever order the clauses were declared in.
        if (_missingInstance == SagaMissingInstance.Fault)
            binding.MissingInstance = SagaMissingInstance.Fault;

        foreach (var (timeoutType, name) in _declaredTimeouts)
            machine.DeclareTimeout(timeoutType, name);
    }
}
