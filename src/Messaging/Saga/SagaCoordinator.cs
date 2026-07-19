using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Runs one message against one saga instance: correlate, load, transition, write. The whole lifecycle lives here —
/// <c>Initially</c> initiates, <c>During</c> orchestrates, a finalizing clause ends it.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
/// <para>
/// <b>Concurrency is optimistic, resolved by replay.</b> Two messages for one instance can be processed at once (the
/// pump runs N workers), so a transition is a read-modify-write that can lose. Losing means
/// <see cref="SagaConcurrencyException"/> from the repository, and the answer is to <b>reload and run the transition
/// again against the fresh state</b> — up to <see cref="SagaOptions.MaxConcurrencyRetries"/> times, after which the
/// exception escapes into the ordinary retry / dead-letter path. A conflict therefore costs a replay and never a lost
/// update; the alternative, a pessimistic lock held across the handler, would put a database lock's lifetime in the
/// hands of arbitrary user code and turn a slow activity into a stalled partition.
/// </para>
/// <para>
/// The price of replay: <b>an activity can run more than once for one message</b>, so activities must be idempotent —
/// the same requirement at-least-once delivery already imposes. Side effects that must not repeat belong behind
/// <c>IInboxProcessor</c> or an idempotent downstream API.
/// </para>
/// <para>
/// <b>Avoiding the conflict entirely:</b> set <see cref="EventEnvelope.PartitionKey"/> to the saga's correlation key on
/// every message that drives it. The pump hashes that key onto one worker, so an instance's messages are processed one
/// at a time in arrival order and never race — conflicts then only come from other processes. Timeouts and anything
/// published through <c>SagaTransitionContext.PublishAsync</c> already carry it.
/// </para>
/// </remarks>
internal sealed class SagaCoordinator<TState>(
    SagaStateMachine<TState> machine,
    ISagaTimeoutScheduler timeouts,
    IOptions<SagaOptions> options,
    TimeProvider timeProvider,
    ILogger<SagaCoordinator<TState>> logger)
    where TState : class, ISagaState, new()
{
    private readonly SagaOptions _options = options.Value;

    /// <summary>Handle one message for this saga.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="context">The event context.</param>
    /// <param name="services">The message's DI scope — the source of the repository and of anything an activity resolves.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask HandleAsync<TEvent>(EventContext<TEvent> context, IServiceProvider services, CancellationToken cancellationToken)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        // Nothing observes this type, or the machine was never validated — either way it is not this saga's message.
        if (machine.FindBinding(typeof(TEvent)) is not SagaEventBinding<TState, TEvent> binding)
            return;

        if (binding.Correlate is not { } correlate)
            return;

        var correlationId = correlate(context.Event, context.Envelope);
        if (string.IsNullOrEmpty(correlationId))
        {
            SagaLog.Uncorrelated(logger, machine.Name, typeof(TEvent).Name, context.MessageId);
            return;
        }

        var repository = services.GetRequiredService<ISagaRepository<TState>>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ApplyAsync(binding, context, repository, services, correlationId, cancellationToken);
                return;
            }
            catch (SagaConcurrencyException exception)
            {
                if (attempt >= Math.Max(1, _options.MaxConcurrencyRetries))
                {
                    SagaLog.ConcurrencyExhausted(logger, exception, machine.Name, correlationId, attempt);
                    throw;
                }

                SagaLog.ConcurrencyConflict(logger, machine.Name, correlationId, attempt);
                await Task.Delay(_options.ConcurrencyRetryDelay, timeProvider, cancellationToken);
            }
        }
    }

    private async ValueTask ApplyAsync<TEvent>(
        SagaEventBinding<TState, TEvent> binding,
        EventContext<TEvent> context,
        ISagaRepository<TState> repository,
        IServiceProvider services,
        string correlationId,
        CancellationToken cancellationToken)
        where TEvent : class, IEvent
    {
        var loaded = await repository.LoadAsync(correlationId, cancellationToken);
        var isNew = loaded is null;

        SagaTransition<TState, TEvent> transition;
        TState state;

        if (loaded is null)
        {
            // No instance: only an Initially clause may create one — a DuringAny clause does not, because an instance
            // that does not exist is not in "any" state. Anything else is an event for a flow that already finished.
            if (!binding.Transitions.TryGetValue(SagaStates.Initial, out var initiating))
            {
                if (binding.MissingInstance == SagaMissingInstance.Fault)
                    throw new InvalidOperationException($"Saga '{machine.Name}' has no instance '{correlationId}' for {typeof(TEvent).Name}, and no clause initiates one.");

                SagaLog.InstanceNotFound(logger, machine.Name, typeof(TEvent).Name, correlationId);
                return;
            }

            transition = initiating;
            state = new TState();
            state.CorrelationId = correlationId;
            state.CurrentState = SagaStates.Initial;
        }
        else
        {
            if (FindTransition(binding, loaded.CurrentState) is not { } existing)
            {
                SagaLog.NoTransition(logger, machine.Name, typeof(TEvent).Name, loaded.CurrentState, correlationId);
                return;
            }

            transition = existing;
            state = loaded;
        }

        if (!TryConsumeTimeoutToken(context, state, correlationId))
            return;

        var transitionContext = new SagaTransitionContext<TState, TEvent>(state, context, services);
        foreach (var activity in transition.Activities)
            await activity(transitionContext, cancellationToken);

        var from = state.CurrentState;
        ApplyTargetState(transitionContext, transition, state);

        var finalized = string.Equals(state.CurrentState, SagaStates.Final, StringComparison.Ordinal);
        if (finalized && state.FinalizedAtUtc is null)
            state.FinalizedAtUtc = timeProvider.GetUtcNow();

        await PersistAsync(repository, state, isNew, finalized, cancellationToken);

        // Timeouts go out only once the new state is durable. Scheduling before the write would leave a timeout in
        // flight for a transition that a concurrency conflict then rolled back and replayed.
        foreach (var pending in transitionContext.PendingTimeouts)
            await timeouts.ScheduleAsync(pending, cancellationToken);

        SagaLog.Transitioned(logger, machine.Name, correlationId, typeof(TEvent).Name, from, state.CurrentState);
    }

    private ValueTask PersistAsync(ISagaRepository<TState> repository, TState state, bool isNew, bool finalized, CancellationToken cancellationToken)
    {
        if (finalized && _options.RemoveOnFinalize)
        {
            // An instance created and finalized by the same message never existed as far as the store is concerned;
            // inserting it only to delete it would be two writes for no observable difference.
            return isNew ? ValueTask.CompletedTask : repository.DeleteAsync(state, cancellationToken);
        }

        return isNew ? repository.InsertAsync(state, cancellationToken) : repository.UpdateAsync(state, cancellationToken);
    }

    /// <summary>The instance's own state first, the <c>DuringAny</c> wildcard second — a declared state wins over the fallback.</summary>
    private static SagaTransition<TState, TEvent>? FindTransition<TEvent>(SagaEventBinding<TState, TEvent> binding, string sourceState)
        where TEvent : class, IEvent
    {
        if (binding.Transitions.TryGetValue(sourceState, out var exact))
            return exact;

        // A retained finalized instance takes no wildcard clause: "any state" means any state the saga is running in,
        // and a finished flow reacting to a late cancellation would restart work that already ended.
        if (string.Equals(sourceState, SagaStates.Final, StringComparison.Ordinal))
            return null;

        return binding.Transitions.TryGetValue(SagaStates.Any, out var wildcard) ? wildcard : null;
    }

    private static void ApplyTargetState<TEvent>(SagaTransitionContext<TState, TEvent> context, SagaTransition<TState, TEvent> transition, TState state)
        where TEvent : class, IEvent
    {
        // Imperative wins over declarative: a branch decided inside an activity knows more than the clause did.
        if (context.Finalized)
        {
            state.CurrentState = SagaStates.Final;
            return;
        }

        if (context.TargetStateOverride is { } overridden)
        {
            state.CurrentState = overridden;
            return;
        }

        if (transition.Finalizes)
        {
            state.CurrentState = SagaStates.Final;
            return;
        }

        if (transition.TargetState is { } target)
            state.CurrentState = target;
    }

    /// <summary>
    /// Drop a timeout the instance no longer expects. A timeout carries the token it was minted with; the instance
    /// holds the twin until the timeout is cancelled or superseded, so a mismatch means this message is stale — the
    /// only way to "unschedule" on a transport that cannot recall an accepted message.
    /// </summary>
    private bool TryConsumeTimeoutToken<TEvent>(EventContext<TEvent> context, TState state, string correlationId)
        where TEvent : class, IEvent
    {
        if (machine.TimeoutNameFor(typeof(TEvent)) is null || !context.Headers.TryGetValue(SagaHeaders.TimeoutToken, out var token))
            return true; // not a saga-scheduled timeout — an ordinary event of the same type is handled normally

        var name = context.Headers.TryGetValue(SagaHeaders.TimeoutName, out var declared) ? declared : typeof(TEvent).Name;
        if (!state.TimeoutTokens.TryGetValue(name, out var expected) || !string.Equals(expected, token, StringComparison.Ordinal))
        {
            SagaLog.StaleTimeout(logger, machine.Name, name, correlationId);
            return false;
        }

        state.TimeoutTokens.Remove(name); // fired once; a redelivery of the same timeout is stale from here on
        return true;
    }
}

/// <summary>
/// Adapts one observed event type onto the saga — an ordinary <see cref="IEventHandler{TEvent}"/>, so a saga consumes
/// through the same pump, filters, retry and dead-lettering as everything else, and shows up in the consumed-type set
/// the broker topology is built from.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
internal sealed class SagaEventHandler<TState, TEvent>(SagaCoordinator<TState> coordinator, IServiceProvider services) : IEventHandler<TEvent>
    where TState : class, ISagaState, new()
    where TEvent : class, IEvent
{
    public ValueTask HandleAsync(EventContext<TEvent> context, CancellationToken cancellationToken)
        => coordinator.HandleAsync(context, services, cancellationToken);
}

/// <summary>Saga log messages. Non-generic on purpose — the logging source generator emits into the declaring type, and the coordinator is generic.</summary>
internal static partial class SagaLog
{
    [LoggerMessage(EventId = 6111, Level = LogLevel.Debug, Message = "Saga {Saga}: {Event} {MessageId} carries no correlation key; ignored")]
    public static partial void Uncorrelated(ILogger logger, string saga, string @event, string messageId);

    [LoggerMessage(EventId = 6112, Level = LogLevel.Debug, Message = "Saga {Saga}: no instance {CorrelationId} for {Event}, and no clause initiates one; ignored")]
    public static partial void InstanceNotFound(ILogger logger, string saga, string @event, string correlationId);

    [LoggerMessage(EventId = 6113, Level = LogLevel.Debug, Message = "Saga {Saga}: {Event} has no clause in state {State} (instance {CorrelationId}); ignored")]
    public static partial void NoTransition(ILogger logger, string saga, string @event, string state, string correlationId);

    [LoggerMessage(EventId = 6114, Level = LogLevel.Debug, Message = "Saga {Saga}: timeout {Timeout} for instance {CorrelationId} was cancelled or superseded; dropped")]
    public static partial void StaleTimeout(ILogger logger, string saga, string timeout, string correlationId);

    [LoggerMessage(EventId = 6115, Level = LogLevel.Debug, Message = "Saga {Saga}: instance {CorrelationId} changed concurrently on attempt {Attempt}; reloading and replaying the transition")]
    public static partial void ConcurrencyConflict(ILogger logger, string saga, string correlationId, int attempt);

    [LoggerMessage(EventId = 6116, Level = LogLevel.Error, Message = "Saga {Saga}: instance {CorrelationId} still contended after {Attempts} attempts; the message goes to retry / dead-letter")]
    public static partial void ConcurrencyExhausted(ILogger logger, Exception exception, string saga, string correlationId, int attempts);

    [LoggerMessage(EventId = 6117, Level = LogLevel.Debug, Message = "Saga {Saga} instance {CorrelationId}: {Event} moved {From} -> {To}")]
    public static partial void Transitioned(ILogger logger, string saga, string correlationId, string @event, string from, string to);
}
