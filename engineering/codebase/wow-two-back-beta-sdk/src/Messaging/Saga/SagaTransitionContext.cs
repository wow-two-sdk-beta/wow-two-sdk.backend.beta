namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

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

        // Persisted with the instance: a timeout whose twin token is gone is dropped on arrival.
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
