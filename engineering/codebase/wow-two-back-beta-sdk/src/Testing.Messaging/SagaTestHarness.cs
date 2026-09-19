using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging.Trackers;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>Entry point for <see cref="SagaTestHarness{TState}"/> — non-generic so both type arguments can be given explicitly.</summary>
public static class SagaTestHarness
{
    /// <summary>
    /// Build and start an in-memory bus with <typeparamref name="TStateMachine"/> registered, its repository wrapped in
    /// a <see cref="SagaRecorder{TState}"/>, and the clock faked.
    /// </summary>
    /// <typeparam name="TStateMachine">The state machine under test.</typeparam>
    /// <typeparam name="TState">The persisted instance state.</typeparam>
    /// <param name="configureServices">Runs before <c>AddSaga</c>: the activities' collaborators, consume filters, fault classification. To swap the repository use <paramref name="repository"/> — a later <c>AddSagaRepository</c> would win over the recorder and silence it.</param>
    /// <param name="configureSaga">Saga runtime options — concurrency retries, <see cref="SagaOptions.RemoveOnFinalize"/>.</param>
    /// <param name="repository">Builds the repository the recorder wraps. Defaults to <see cref="InMemorySagaRepository{TState}"/>; pass a factory to put a durable or fault-injecting store underneath.</param>
    /// <param name="configureBus">In-memory transport options — channel capacity, retry schedule.</param>
    /// <param name="handlerAssemblies">Assemblies scanned for plain <see cref="IEventHandler{TEvent}"/>s alongside the saga; defaults to the state machine's own assembly.</param>
    /// <param name="options">Harness timings and clock start. Defaults to <see cref="SagaHarnessOptions"/>'s own defaults.</param>
    /// <param name="cancellationToken">Cancellation token for host startup.</param>
    /// <returns>A started harness. Dispose it (<c>await using</c>) to stop the host.</returns>
    /// <remarks>
    ///   - <paramref name="configureSaga"/> and <paramref name="configureBus"/> run after the harness defaults, so a test can set a real schedule back
    /// </remarks>
    public static async Task<SagaTestHarness<TState>> StartAsync<TStateMachine, TState>(
        Action<IServiceCollection>? configureServices = null,
        Action<SagaOptions>? configureSaga = null,
        Func<IServiceProvider, ISagaRepository<TState>>? repository = null,
        Action<InMemoryEventBusOptions>? configureBus = null,
        Assembly[]? handlerAssemblies = null,
        SagaHarnessOptions? options = null,
        CancellationToken cancellationToken = default)
        where TStateMachine : SagaStateMachine<TState>, new()
        where TState : class, ISagaState, new()
    {
        var settings = options ?? new SagaHarnessOptions();
        var time = new FakeTimeProvider(settings.StartTime);
        var recorder = new SagaRecorder<TState>();

        var messaging = await MessagingTestHarness.StartAsync(
            services =>
            {
                // Everything time-driven reads one clock, so the registered TimeProvider is replaced with the fake.
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time));

                configureServices?.Invoke(services);
                services.AddSaga<TStateMachine, TState>(saga =>
                {
                    // Under the faked clock a retry delay only elapses on a test advance, deadlocking the replay.
                    saga.ConcurrencyRetryDelay = TimeSpan.Zero;
                    configureSaga?.Invoke(saga);
                });

                // Last, and closed over TState, so the decorator wins over AddSaga's open-generic repository.
                services.AddSagaRecorder(recorder, repository);
            },
            bus =>
            {
                // Under the faked clock a backoff never elapses, so a faulting message never reaches attempt two.
                bus.Retry = bus.Retry with { Backoff = BackoffKind.None };
                configureBus?.Invoke(bus);
            },
            handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [typeof(TStateMachine).Assembly],
            settings.Messaging,
            cancellationToken);

        return new SagaTestHarness<TState>(messaging, recorder, time, settings);
    }
}

/// <summary>
/// A running saga plus the assertions to interrogate it: which state an instance is in, which edges it took, when it
/// finalized, and where a version conflict made it replay. Turns a saga test into
/// arrange harness → publish → await a state.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
///   - the clock is always a <see cref="FakeTimeProvider"/>
///   - advance <see cref="Time"/>, or fire a parked timeout with <see cref="FireTimeoutAsync"/>
///   - a faked clock never elapses a backoff, so <see cref="SagaOptions.ConcurrencyRetryDelay"/> and the consume retry backoff are zeroed, never the attempt budgets
/// </remarks>
public sealed class SagaTestHarness<TState> : IAsyncDisposable
    where TState : class, ISagaState, new()
{
    private readonly SagaHarnessOptions _options;

    internal SagaTestHarness(MessagingTestHarness messaging, SagaRecorder<TState> recorder, FakeTimeProvider time, SagaHarnessOptions options)
    {
        Messaging = messaging;
        Recorder = recorder;
        Time = time;
        _options = options;

        var resolved = messaging.Services.GetRequiredService<ISagaRepository<TState>>();
        Repository = resolved is RecordingSagaRepository<TState> recording ? recording.Inner : resolved;
    }

    /// <summary>The message harness underneath — <c>Published</c>, <c>Consumed</c>, <c>Faulted</c>, <c>DeadLettered</c>, <c>WaitForIdleAsync</c>.</summary>
    public MessagingTestHarness Messaging { get; }

    /// <summary>The saga's transition recorder.</summary>
    public SagaRecorder<TState> Recorder { get; }

    /// <summary>The harness's clock. Advance it to fire a scheduled timeout; <see cref="FireTimeoutAsync"/> is the version that knows how far.</summary>
    public FakeTimeProvider Time { get; }

    /// <summary>
    /// The store itself, under the recorder. Reads through it are invisible to <see cref="Transitions"/>, which is what
    /// makes an assertion on current state safe to run mid-flow.
    /// </summary>
    public ISagaRepository<TState> Repository { get; }

    /// <summary>Every reaction the saga had, in order.</summary>
    public RecordedTransitionTracker<TState> Transitions => Recorder.Transitions;

    /// <summary>The host's service provider.</summary>
    public IServiceProvider Services => Messaging.Services;

    /// <summary>The bus under test.</summary>
    public IEventBus Bus => Messaging.Bus;

    /// <summary>Envelopes the transport accepted — including every scheduled timeout.</summary>
    public RecordedMessageTracker Published => Messaging.Published;

    /// <summary>Delivery attempts that completed.</summary>
    public RecordedMessageTracker Consumed => Messaging.Consumed;

    /// <summary>Delivery attempts that threw — where a <see cref="SagaMissingInstance.Fault"/> or an exhausted concurrency budget lands.</summary>
    public RecordedMessageTracker Faulted => Messaging.Faulted;

    /// <summary>Messages that exhausted processing and were dead-lettered.</summary>
    public RecordedMessageTracker DeadLettered => Messaging.DeadLettered;

    /// <summary>Every saga timeout currently on the wire, in publish order.</summary>
    public IReadOnlyList<RecordedSagaTimeout> ScheduledTimeouts
        => [.. Messaging.Published.All.Where(static message => IsTimeout(message, name: null, correlationId: null)).Select(ToTimeout)];

    /// <summary>The instance for a correlation id, or null when the store holds none — because it never existed, or because it finalized and was removed.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask<TState?> GetInstanceAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return Repository.LoadAsync(correlationId, cancellationToken);
    }

    /// <summary>The state an instance is currently in, or null when the store holds none.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>A finalized instance under <see cref="SagaOptions.RemoveOnFinalize"/> is gone, so this reads null — assert the terminal state on <see cref="Transitions"/>, which keeps it.</remarks>
    public async ValueTask<string?> CurrentStateAsync(string correlationId, CancellationToken cancellationToken = default)
        => (await GetInstanceAsync(correlationId, cancellationToken))?.CurrentState;

    /// <summary>How many instances the store holds — running ones, plus finalized ones still retained.</summary>
    /// <exception cref="InvalidOperationException">The repository under the recorder is not the in-memory default, which is the only one that exposes a count.</exception>
    public int CountInstances()
        => Repository as InMemorySagaRepository<TState> is { } store
            ? store.Count
            : throw new InvalidOperationException($"Counting instances needs the default {nameof(InMemorySagaRepository<TState>)}; this harness runs on a {Repository.GetType().Name}. Read it through that repository instead.");

    /// <summary>Sweep instances finalized longer ago than <paramref name="retention"/>; returns how many went. Only meaningful with <see cref="SagaOptions.RemoveOnFinalize"/> off.</summary>
    /// <param name="retention">How long a finalized instance is kept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask<int> PurgeFinalizedAsync(TimeSpan retention, CancellationToken cancellationToken = default)
        => Repository.PurgeFinalizedAsync(retention, cancellationToken);

    /// <summary>Whether a <typeparamref name="TEvent"/> moved an instance from <paramref name="from"/> to <paramref name="to"/> — <c>null</c> means "any" on every part.</summary>
    /// <typeparam name="TEvent">The event that drove the transition.</typeparam>
    /// <param name="from">The source state, or <c>null</c> for any. A created instance transitions from <see cref="SagaStateConstants.Initial"/>.</param>
    /// <param name="to">The target state, or <c>null</c> for any.</param>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for any.</param>
    public bool HasTransition<TEvent>(string? from = null, string? to = null, string? correlationId = null)
        where TEvent : class, IEvent
        => Transitions.Has<TEvent>(from, to, correlationId);

    /// <summary>Wait until an instance is written in <paramref name="state"/>.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="state">The state to wait for.</param>
    /// <param name="timeout">Overall budget. Defaults to <see cref="RecordedTransitionTracker{TState}.DefaultTimeout"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transition that got it there.</returns>
    /// <exception cref="TimeoutException">The instance never reached that state — the message names the edges it did take.</exception>
    public async Task<RecordedTransition<TState>> WaitForStateAsync(string correlationId, string state, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var matches = await Transitions.WaitForAsync(
            transition => transition.Outcome == SagaTransitionOutcome.Transitioned
                && string.Equals(transition.CorrelationId, correlationId, StringComparison.Ordinal)
                && string.Equals(transition.ToState, state, StringComparison.Ordinal),
            count: 1,
            what: $"instance '{correlationId}' reaching '{state}'",
            timeout,
            cancellationToken);

        return matches[0];
    }

    /// <summary>Wait until an instance reaches <see cref="SagaStateConstants.Final"/> — whether it is then removed or retained.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="timeout">Overall budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<RecordedTransition<TState>> WaitForFinalizedAsync(string correlationId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => WaitForStateAsync(correlationId, SagaStateConstants.Final, timeout, cancellationToken);

    /// <summary>Wait for a from-state → event → to-state edge to be taken.</summary>
    /// <typeparam name="TEvent">The event that drives it.</typeparam>
    /// <param name="from">The source state, or <c>null</c> for any.</param>
    /// <param name="to">The target state, or <c>null</c> for any.</param>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for any.</param>
    /// <param name="timeout">Overall budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RecordedTransition<TState>> WaitForTransitionAsync<TEvent>(
        string? from = null,
        string? to = null,
        string? correlationId = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        var matches = await Transitions.WaitForAsync(
            transition => transition.Outcome == SagaTransitionOutcome.Transitioned
                && transition.Is<TEvent>()
                && (from is null || string.Equals(transition.FromState, from, StringComparison.Ordinal))
                && (to is null || string.Equals(transition.ToState, to, StringComparison.Ordinal))
                && (correlationId is null || string.Equals(transition.CorrelationId, correlationId, StringComparison.Ordinal)),
            count: 1,
            what: $"{from ?? "*"} --{typeof(TEvent).Name}--> {to ?? "*"}",
            timeout,
            cancellationToken);

        return matches[0];
    }

    /// <summary>
    /// Wait until a transition has landed after losing an optimistic-concurrency race — the behaviour that fails
    /// silently, because a repository that skips its version check produces the same published events and the same final
    /// state while quietly dropping the concurrent writer's update.
    /// </summary>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for any.</param>
    /// <param name="count">How many replayed transitions to wait for. Default 1.</param>
    /// <param name="timeout">Overall budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<RecordedTransition<TState>>> WaitForReplayAsync(
        string? correlationId = null,
        int count = 1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => Transitions.WaitForAsync(
            transition => transition.Outcome == SagaTransitionOutcome.Transitioned
                && transition.Replayed
                && (correlationId is null || string.Equals(transition.CorrelationId, correlationId, StringComparison.Ordinal)),
            count,
            what: "a transition replayed after a version conflict",
            timeout,
            cancellationToken);

    /// <summary>How many writes the repository rejected on its version check.</summary>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for all.</param>
    public int ConcurrencyConflicts(string? correlationId = null) => Recorder.ConcurrencyConflicts(correlationId);

    /// <summary>How many transitions landed only after being re-run against reloaded state.</summary>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for all.</param>
    public int Replays(string? correlationId = null) => Recorder.Replays(correlationId);

    /// <summary>Wait until a scheduled timeout is on the wire — which is what proves the transport has parked it, and therefore that advancing the clock is deterministic.</summary>
    /// <param name="name">The timeout's declared name, or <c>null</c> for any.</param>
    /// <param name="correlationId">The instance it is addressed to, or <c>null</c> for any.</param>
    /// <param name="timeout">Overall budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RecordedSagaTimeout> WaitForTimeoutAsync(
        string? name = null,
        string? correlationId = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var matches = await Messaging.Published.WaitForAsync(
            message => IsTimeout(message, name, correlationId),
            count: 1,
            timeout,
            cancellationToken);

        return ToTimeout(matches[0]);
    }

    /// <summary>
    /// Drive a scheduled timeout to delivery: wait for it to be parked, advance the clock exactly to its due time, and
    /// return once the saga has consumed that very message. No step sleeps or guesses.
    /// </summary>
    /// <param name="name">The timeout's declared name, or <c>null</c> for the first one scheduled.</param>
    /// <param name="correlationId">The instance it is addressed to, or <c>null</c> for any.</param>
    /// <param name="overshoot">How far past the due time to advance. Defaults to <see cref="SagaHarnessOptions.TimeoutOvershoot"/>.</param>
    /// <param name="timeout">Overall budget for each of the two waits.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The timeout as it went out.</returns>
    /// <remarks>
    ///   - a cancelled or superseded timeout arrives with a stale token and is dropped
    ///   - returns either way
    ///   - <see cref="Transitions"/> is where the difference shows
    /// </remarks>
    public async Task<RecordedSagaTimeout> FireTimeoutAsync(
        string? name = null,
        string? correlationId = null,
        TimeSpan? overshoot = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var scheduled = await WaitForTimeoutAsync(name, correlationId, timeout, cancellationToken);
        if (scheduled.DueUtc is not { } due)
            throw new InvalidOperationException($"Timeout '{scheduled.Name}' carries no delivery time, so there is nothing to advance to — the transport published it immediately.");

        var delta = due - Time.GetUtcNow() + (overshoot ?? _options.TimeoutOvershoot);
        if (delta > TimeSpan.Zero)
            Time.Advance(delta);

        // The scheduler re-enqueues the parked envelope, so the delivery carries the same message id.
        await Messaging.Consumed.WaitForAsync(
            message => string.Equals(message.MessageId, scheduled.MessageId, StringComparison.Ordinal),
            count: 1,
            timeout,
            cancellationToken);

        // A dropped timeout writes nothing, so its record lands only when the message finishes.
        await Transitions.WaitForAsync(
            transition => string.Equals(transition.Envelope?.MessageId, scheduled.MessageId, StringComparison.Ordinal),
            count: 1,
            what: $"the saga's reaction to timeout '{scheduled.Name}'",
            timeout,
            cancellationToken);

        return scheduled;
    }

    /// <summary>Advance the clock and wait for whatever that set off to finish.</summary>
    /// <param name="delta">How far to advance.</param>
    /// <param name="quietPeriod">How long the bus must be silent to count as idle.</param>
    /// <param name="timeout">Overall budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>The blunt instrument, for asserting a timeout did not fire. To fire one, prefer <see cref="FireTimeoutAsync"/> — it waits on the message rather than on silence.</remarks>
    public async Task AdvanceAsync(TimeSpan delta, TimeSpan? quietPeriod = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Time.Advance(delta);
        await Messaging.WaitForIdleAsync(quietPeriod, timeout, cancellationToken);
    }

    /// <summary>Wait until the bus has gone quiet and nothing is in flight — the primitive for asserting the saga did not react.</summary>
    /// <param name="quietPeriod">How long the silence must last.</param>
    /// <param name="timeout">Overall budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task WaitForIdleAsync(TimeSpan? quietPeriod = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Messaging.WaitForIdleAsync(quietPeriod, timeout, cancellationToken);

    /// <summary>Empty every log — messages and transitions — for a second phase of the same test. Stored instances are left alone.</summary>
    public void Reset()
    {
        Messaging.Reset();
        Recorder.Reset();
    }

    /// <summary>Stop and dispose the host.</summary>
    public ValueTask DisposeAsync() => Messaging.DisposeAsync();

    private static bool IsTimeout(RecordedMessage message, string? name, string? correlationId)
    {
        var headers = message.Envelope.Headers;
        return headers.ContainsKey(SagaHeaderConstants.TimeoutToken)
            && (name is null || (headers.TryGetValue(SagaHeaderConstants.TimeoutName, out var declared) && string.Equals(declared, name, StringComparison.Ordinal)))
            && (correlationId is null || string.Equals(message.Envelope.CorrelationId, correlationId, StringComparison.Ordinal));
    }

    private static RecordedSagaTimeout ToTimeout(RecordedMessage message)
    {
        var envelope = message.Envelope;
        return new RecordedSagaTimeout
        {
            Name = envelope.Headers.TryGetValue(SagaHeaderConstants.TimeoutName, out var name) ? name : envelope.BodyType.Name,
            Token = envelope.Headers.TryGetValue(SagaHeaderConstants.TimeoutToken, out var token) ? token : string.Empty,
            CorrelationId = envelope.CorrelationId ?? string.Empty,
            DueUtc = envelope.NotBeforeUtc,
            Envelope = envelope,
        };
    }
}
