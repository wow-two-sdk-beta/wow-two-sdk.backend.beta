using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// The harness's eyes on one saga's instances — every reaction to every message, as a
/// <see cref="RecordedTransition{TState}"/>.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
/// <para>
/// <b>Why this is not the message recorder again.</b> <see cref="MessagingRecorder"/> is built on the observer seam,
/// which sees messages crossing the bus. A saga transition never crosses it: the coordinator loads state, runs the
/// transition in-process and saves. So the seam here is the <see cref="ISagaRepository{TState}"/> — the one boundary
/// every transition necessarily crosses, and the only part of the saga runtime that is public
/// (<c>SagaCoordinator</c> and <c>ISagaTimeoutScheduler</c> are both internal to the core).
/// </para>
/// <para>
/// A repository call on its own says which instance moved but not what moved it, so the recorder pairs it with the
/// message being consumed at the time, carried on an ambient set by a pass-through <see cref="IConsumeFilter"/> — the
/// one seam that brackets the handler, and therefore the coordinator, on the same async flow. The filter calls
/// <c>next</c> unconditionally and the repository delegates every operation: neither changes routing, settlement or
/// concurrency behaviour.
/// </para>
/// <para>
/// <b>Ordering.</b> A transition that writes is recorded at the write, inside the handler. One that writes nothing —
/// <see cref="SagaTransitionOutcome.Ignored"/>, <see cref="SagaTransitionOutcome.Faulted"/> — is only knowable when the
/// message finishes, so it is recorded when the filter's bracket closes, which is <i>after</i> the consume observers
/// have run. A test that awaits <see cref="MessagingTestHarness.Consumed"/> and then reads the log can therefore be a
/// beat early: wait on <see cref="Transitions"/> instead, which signals on the record itself.
/// </para>
/// <para>
/// <b>The one blind spot.</b> An instance created <i>and</i> finalized by a single message under
/// <see cref="SagaOptions.RemoveOnFinalize"/> is never written — the coordinator skips an insert it would immediately
/// delete — so it records as <see cref="SagaTransitionOutcome.Ignored"/>. Turn <c>RemoveOnFinalize</c> off in that
/// test and the terminal write, and with it the transition, becomes visible.
/// </para>
/// <para>Registered as a singleton and hit from every consume worker — every member is thread-safe.</para>
/// </remarks>
public sealed class SagaRecorder<TState>
    where TState : class, ISagaState
{
    /// <summary>Every reaction recorded so far — awaitable, so a test waits for a state rather than sleeping toward it.</summary>
    public RecordedTransitionLog<TState> Transitions { get; } = new();

    /// <summary>
    /// How many writes were rejected by the repository's version check. Each one costs a replay, never a lost update —
    /// a count that stays at zero in a test built to force a conflict means the repository is not enforcing its
    /// contract, which is the failure this whole path exists to catch.
    /// </summary>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for all.</param>
    public int ConcurrencyConflicts(string? correlationId = null)
        => Transitions.Count(transition => transition.Outcome == SagaTransitionOutcome.Conflicted && Matches(transition, correlationId));

    /// <summary>How many transitions landed only after being re-run against reloaded state.</summary>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for all.</param>
    public int Replays(string? correlationId = null)
        => Transitions.Count(transition => transition.Outcome == SagaTransitionOutcome.Transitioned && transition.Replayed && Matches(transition, correlationId));

    /// <summary>Empty the log — for a second phase of the same test.</summary>
    public void Reset() => Transitions.Clear();

    /// <summary>A one-line census of the edges taken — the payload of a harness timeout message.</summary>
    public override string ToString() => Transitions.ToString();

    internal void Append(RecordedTransition<TState> transition) => Transitions.Append(transition);

    private static bool Matches(RecordedTransition<TState> transition, string? correlationId)
        => correlationId is null || string.Equals(transition.CorrelationId, correlationId, StringComparison.Ordinal);
}

/// <summary>DI registration for the saga test recorder.</summary>
public static class SagaTestingServiceCollectionExtensions
{
    /// <summary>
    /// Wrap one saga's repository in the recorder, so every load / write the coordinator makes is observable. Use this
    /// to watch a host the test built itself; <see cref="SagaTestHarness.StartAsync{TStateMachine,TState}"/> already
    /// does it.
    /// </summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="recorder">The recorder to register — hold the reference before the host exists.</param>
    /// <param name="repository">
    /// Builds the repository under the recorder. Defaults to a fresh <see cref="InMemorySagaRepository{TState}"/>,
    /// which is what <c>AddSaga</c> would have registered; pass a factory to put a durable or fault-injecting
    /// repository underneath instead.
    /// </param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Call it <b>after</b> <c>AddSaga</c>. The decorator is registered closed over <typeparamref name="TState"/>, and a
    /// closed registration wins over the open-generic default <c>AddSaga</c> adds — which also means a later
    /// <c>AddSagaRepository&lt;TState, …&gt;</c> would win over this one and silence the recorder.
    /// </remarks>
    public static IServiceCollection AddSagaRecorder<TState>(
        this IServiceCollection services,
        SagaRecorder<TState> recorder,
        Func<IServiceProvider, ISagaRepository<TState>>? repository = null)
        where TState : class, ISagaState
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(recorder);

        services.TryAddSingleton(recorder);

        // TryAddEnumerable, not AddConsumeFilter<T>(): registering the bracket twice would nest it, and the inner
        // bracket would shadow the outer one's envelope for half the pipeline.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConsumeFilter, SagaConsumeBracketFilter>());

        repository ??= static provider => new InMemorySagaRepository<TState>(provider.GetRequiredService<TimeProvider>());
        services.AddSingleton<ISagaRepository<TState>>(provider => new RecordingSagaRepository<TState>(repository(provider), recorder));
        return services;
    }
}

/// <summary>
/// Delegating <see cref="ISagaRepository{TState}"/> that reconstructs a transition from the calls the coordinator makes:
/// a load pends one, the following write completes it, and a load with no write is the ignored path.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class RecordingSagaRepository<TState>(ISagaRepository<TState> inner, SagaRecorder<TState> recorder) : ISagaRepository<TState>
    where TState : class, ISagaState
{
    /// <summary>The repository under the recorder — the store itself, for a read that must not show up as a transition.</summary>
    public ISagaRepository<TState> Inner => inner;

    public async ValueTask<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken)
    {
        var loaded = await inner.LoadAsync(correlationId, cancellationToken);

        if (SagaConsumeBracket.Current is { } bracket)
        {
            // A copy: the coordinator mutates what it loaded, and the from-state has to survive that.
            bracket.Pend(
                (typeof(TState), correlationId),
                new SagaPendingWrite<TState>
                {
                    Recorder = recorder,
                    CorrelationId = correlationId,
                    FromState = loaded?.CurrentState,
                    Loaded = loaded is null ? null : (TState)loaded.Copy(),
                    LoadedVersion = loaded?.Version ?? 0,
                    Envelope = bracket.Envelope,
                });
        }

        return loaded;
    }

    public ValueTask InsertAsync(TState state, CancellationToken cancellationToken)
        => WriteAsync(state, inner.InsertAsync, created: true, removed: false, cancellationToken);

    public ValueTask UpdateAsync(TState state, CancellationToken cancellationToken)
        => WriteAsync(state, inner.UpdateAsync, created: false, removed: false, cancellationToken);

    public ValueTask DeleteAsync(TState state, CancellationToken cancellationToken)
        => WriteAsync(state, inner.DeleteAsync, created: false, removed: true, cancellationToken);

    // Not a transition — a sweep of instances that already finalized. Passed straight through so a retention test reads
    // the real count.
    public ValueTask<int> PurgeFinalizedAsync(TimeSpan retention, CancellationToken cancellationToken)
        => inner.PurgeFinalizedAsync(retention, cancellationToken);

    private async ValueTask WriteAsync(
        TState state,
        Func<TState, CancellationToken, ValueTask> write,
        bool created,
        bool removed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var bracket = SagaConsumeBracket.Current;

        // The key carries TState, so nothing but this state type's pending load can come back.
        var pending = bracket?.Take((typeof(TState), state.CorrelationId)) as SagaPendingWrite<TState>;

        try
        {
            await write(state, cancellationToken);
        }
        catch (SagaConcurrencyException exception)
        {
            // Recorded, then rethrown untouched: the coordinator's own reload-and-replay is what the next record proves
            // happened, and swallowing this here would remove the behaviour under test.
            Record(state, pending, bracket, SagaTransitionOutcome.Conflicted, created: false, removed: false, exception);
            throw;
        }

        Record(state, pending, bracket, SagaTransitionOutcome.Transitioned, created, removed, exception: null);
    }

    private void Record(
        TState state,
        SagaPendingWrite<TState>? pending,
        SagaConsumeBracket? bracket,
        SagaTransitionOutcome outcome,
        bool created,
        bool removed,
        Exception? exception)
        => recorder.Append(new RecordedTransition<TState>
        {
            CorrelationId = state.CorrelationId,

            // An insert has nothing loaded behind it, but the instance is not stateless: the coordinator starts it in
            // SagaStates.Initial, which is the state the Initially clause is bound to and the one a test asserts from.
            FromState = pending?.FromState ?? (created ? SagaStates.Initial : null),
            ToState = state.CurrentState,
            Outcome = outcome,
            Attempt = pending?.Attempt ?? 1,
            Envelope = bracket?.Envelope,
            Instance = (TState)state.Copy(),
            Version = state.Version,
            Created = created,
            Removed = removed,
            Exception = exception,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        });
}

/// <summary>A load waiting for the write that will complete it — or for the end of the message, which is what makes it an ignored event.</summary>
internal interface ISagaPendingWrite
{
    /// <summary>Which pass over this instance produced it, from 1. Assigned by the bracket, which owns the counter.</summary>
    int Attempt { get; set; }

    /// <summary>Record the load as a non-write outcome.</summary>
    /// <param name="outcome">The outcome to record.</param>
    /// <param name="exception">The failure, when the message threw.</param>
    void Flush(SagaTransitionOutcome outcome, Exception? exception);
}

/// <summary>Typed pending load — holds the from-state until a write pairs with it.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class SagaPendingWrite<TState> : ISagaPendingWrite
    where TState : class, ISagaState
{
    public required SagaRecorder<TState> Recorder { get; init; }

    public required string CorrelationId { get; init; }

    public required string? FromState { get; init; }

    public required TState? Loaded { get; init; }

    public required int LoadedVersion { get; init; }

    public required EventEnvelope? Envelope { get; init; }

    public int Attempt { get; set; }

    public void Flush(SagaTransitionOutcome outcome, Exception? exception)
        => Recorder.Append(new RecordedTransition<TState>
        {
            CorrelationId = CorrelationId,
            FromState = FromState,
            ToState = FromState, // nothing was written, so the instance is where it was
            Outcome = outcome,
            Attempt = Attempt,
            Envelope = Envelope,
            Instance = Loaded,
            Version = LoadedVersion,
            Exception = exception,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        });
}

/// <summary>
/// One message's worth of saga bookkeeping: the envelope being consumed, plus the loads that have not yet been paired
/// with a write. Ambient for the duration of the consume, so the repository — which the coordinator resolves from the
/// message scope — can see which event it is serving.
/// </summary>
internal sealed class SagaConsumeBracket(EventEnvelope envelope)
{
    private static readonly AsyncLocal<SagaConsumeBracket?> Slot = new();

    private readonly Lock _sync = new();
    private readonly Dictionary<(Type StateType, string CorrelationId), ISagaPendingWrite> _pending = [];
    private readonly Dictionary<(Type StateType, string CorrelationId), int> _attempts = [];

    /// <summary>The bracket for the message on this async flow, or null outside the consume pipeline.</summary>
    public static SagaConsumeBracket? Current
    {
        get => Slot.Value;
        set => Slot.Value = value;
    }

    /// <summary>The message being consumed.</summary>
    public EventEnvelope Envelope => envelope;

    /// <summary>Register a load, stamping it with this instance's next attempt number.</summary>
    public void Pend((Type StateType, string CorrelationId) key, ISagaPendingWrite pending)
    {
        lock (_sync)
        {
            _attempts.TryGetValue(key, out var attempt);
            _attempts[key] = pending.Attempt = attempt + 1;

            // Replaces rather than flushes any predecessor: a delivery retry re-loads the same instance, and each pass
            // that wrote nothing is the same non-event as the one before it.
            _pending[key] = pending;
        }
    }

    /// <summary>Take the pending load for an instance — the write is about to complete it.</summary>
    public ISagaPendingWrite? Take((Type StateType, string CorrelationId) key)
    {
        lock (_sync)
            return _pending.Remove(key, out var pending) ? pending : null;
    }

    /// <summary>Record every load the message never paired with a write.</summary>
    /// <param name="outcome">The outcome to record them under.</param>
    /// <param name="exception">The failure, when the message threw.</param>
    public void FlushPending(SagaTransitionOutcome outcome, Exception? exception)
    {
        ISagaPendingWrite[] leftovers;
        lock (_sync)
        {
            leftovers = [.. _pending.Values];
            _pending.Clear();
        }

        foreach (var leftover in leftovers)
            leftover.Flush(outcome, exception);
    }
}

/// <summary>
/// Pass-through consume filter that opens a <see cref="SagaConsumeBracket"/> around the message. A filter, not an
/// observer: an observer's hooks return before the handler runs, so an ambient it sets never reaches the coordinator,
/// while a filter wraps the whole dispatch on one async flow.
/// </summary>
internal sealed class SagaConsumeBracketFilter : IConsumeFilter
{
    public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (SagaConsumeBracket.Current is not null)
        {
            await next(context, cancellationToken); // already bracketed — nesting would shadow the outer envelope
            return;
        }

        var bracket = new SagaConsumeBracket(context.Envelope);
        SagaConsumeBracket.Current = bracket;
        try
        {
            await next(context, cancellationToken);
            bracket.FlushPending(SagaTransitionOutcome.Ignored, exception: null);
        }
        catch (Exception exception)
        {
            // The message is on its way to retry or dead-letter; the saga's own view of it is that the instance never
            // moved. Rethrown untouched — a filter that swallowed here would change settlement.
            bracket.FlushPending(SagaTransitionOutcome.Faulted, exception);
            throw;
        }
        finally
        {
            SagaConsumeBracket.Current = null;
        }
    }
}
