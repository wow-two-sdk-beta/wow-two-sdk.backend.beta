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
///   - concurrency is optimistic, resolved by replay — the cap, the escape and the remedies are in <c>Saga.md</c>
///   - a replay can run an activity more than once for one message, so keep every activity idempotent
///   - set <see cref="EventEnvelope.PartitionKey"/> to the correlation key so an instance's messages never race
/// </remarks>
internal sealed class SagaCoordinator<TState>(
    SagaStateMachine<TState> machine,
    ISagaTimeoutService timeouts,
    SagaOptions options,
    TimeProvider timeProvider,
    ILogger<SagaCoordinator<TState>> logger)
    where TState : class, ISagaState, new()
{
    private readonly SagaOptions _options = options;

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
            // No instance: only an Initially clause creates one, never a DuringAny clause.
            if (!binding.Transitions.TryGetValue(SagaStateConstants.Initial, out var initiating))
            {
                if (binding.MissingInstance == SagaMissingInstance.Fault)
                    throw new InvalidOperationException($"Saga '{machine.Name}' has no instance '{correlationId}' for {typeof(TEvent).Name}, and no clause initiates one.");

                SagaLog.InstanceNotFound(logger, machine.Name, typeof(TEvent).Name, correlationId);
                return;
            }

            transition = initiating;
            state = new TState();
            state.CorrelationId = correlationId;
            state.CurrentState = SagaStateConstants.Initial;
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

        var finalized = string.Equals(state.CurrentState, SagaStateConstants.Final, StringComparison.Ordinal);
        if (finalized && state.FinalizedAtUtc is null)
            state.FinalizedAtUtc = timeProvider.GetUtcNow();

        await PersistAsync(repository, state, isNew, finalized, cancellationToken);

        // Schedule only once the new state is durable, so a rolled-back transition leaves no timeout in flight.
        foreach (var pending in transitionContext.PendingTimeouts)
            await timeouts.ScheduleAsync(pending, cancellationToken);

        SagaLog.Transitioned(logger, machine.Name, correlationId, typeof(TEvent).Name, from, state.CurrentState);
    }

    private ValueTask PersistAsync(ISagaRepository<TState> repository, TState state, bool isNew, bool finalized, CancellationToken cancellationToken)
    {
        if (finalized && _options.RemoveOnFinalize)
        {
            // An instance created and finalized by one message is never written to the store.
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

        // A finalized instance takes no wildcard clause, so a late event cannot restart a finished flow.
        if (string.Equals(sourceState, SagaStateConstants.Final, StringComparison.Ordinal))
            return null;

        return binding.Transitions.TryGetValue(SagaStateConstants.Any, out var wildcard) ? wildcard : null;
    }

    private static void ApplyTargetState<TEvent>(SagaTransitionContext<TState, TEvent> context, SagaTransition<TState, TEvent> transition, TState state)
        where TEvent : class, IEvent
    {
        // Imperative wins over declarative: a branch decided inside an activity knows more than the clause did.
        if (context.Finalized)
        {
            state.CurrentState = SagaStateConstants.Final;
            return;
        }

        if (context.TargetStateOverride is { } overridden)
        {
            state.CurrentState = overridden;
            return;
        }

        if (transition.Finalizes)
        {
            state.CurrentState = SagaStateConstants.Final;
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
        if (machine.TimeoutNameFor(typeof(TEvent)) is null || !context.Headers.TryGetValue(SagaHeaderConstants.TimeoutToken, out var token))
            return true; // not a saga-scheduled timeout — an ordinary event of the same type is handled normally

        var name = context.Headers.TryGetValue(SagaHeaderConstants.TimeoutName, out var declared) ? declared : typeof(TEvent).Name;
        if (!state.TimeoutTokens.TryGetValue(name, out var expected) || !string.Equals(expected, token, StringComparison.Ordinal))
        {
            SagaLog.StaleTimeout(logger, machine.Name, name, correlationId);
            return false;
        }

        state.TimeoutTokens.Remove(name); // fired once; a redelivery of the same timeout is stale from here on
        return true;
    }
}
