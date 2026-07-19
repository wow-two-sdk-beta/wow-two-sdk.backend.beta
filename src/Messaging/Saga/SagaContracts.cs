using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Well-known saga state names. Every other state is an application-defined string.</summary>
/// <remarks>
/// States are plain strings, not objects: the current state is persisted verbatim in
/// <see cref="ISagaState.CurrentState"/>, so a stored instance stays readable in the database and survives a rename of
/// the C# member that declared it. The cost is that a typo in a state name is a runtime miss (the event finds no
/// transition and is ignored) rather than a compile error — declare states as <c>const</c> on the state machine.
/// </remarks>
public static class SagaStates
{
    /// <summary>The state an instance is in before its first transition — what <c>Initially</c> binds to.</summary>
    public const string Initial = "initial";

    /// <summary>The terminal state. Reaching it finalizes the instance: removed, or retained per <see cref="SagaOptions.RemoveOnFinalize"/>.</summary>
    public const string Final = "final";

    /// <summary>Wildcard source state used by <c>DuringAny</c> — matches an instance in any state. Do not name a real state this.</summary>
    public const string Any = "*";
}

/// <summary>Reserved transport headers a scheduled saga timeout carries, so an instance can tell a live timeout from a cancelled or superseded one.</summary>
/// <remarks>
/// Both use the SDK's reserved <c>wt-</c> namespace: they describe the message the SDK itself produced, and the header
/// propagation policy blocks reserved keys from being copied onto anything published while handling it.
/// </remarks>
public static class SagaHeaders
{
    /// <summary>The timeout's declared name — the key its token is stored under in <see cref="ISagaState.TimeoutTokens"/>.</summary>
    public const string TimeoutName = Transport.MessageHeaders.ReservedPrefix + "saga-timeout-name";

    /// <summary>
    /// The token minted when the timeout was scheduled. A timeout whose token no longer matches the one on the instance
    /// was cancelled or replaced while in flight, and is dropped — this is what makes "unschedule" work on a transport
    /// that cannot recall a message it already accepted.
    /// </summary>
    public const string TimeoutToken = Transport.MessageHeaders.ReservedPrefix + "saga-timeout-token";
}

/// <summary>What to do with an event whose saga instance does not exist and which no <c>Initially</c> clause would create.</summary>
public enum SagaMissingInstance
{
    /// <summary>Drop the event. The default: an event correlating to nothing usually belongs to a flow that already finalized.</summary>
    Ignore = 0,

    /// <summary>Throw, so the message takes the normal retry / dead-letter path. Use where a missing instance means the events arrived out of order.</summary>
    Fault = 1,
}

/// <summary>
/// The persisted state of one saga instance. Implement it on a POCO (or derive from <see cref="SagaState"/>, which
/// implements every member) — the same object is what a repository stores.
/// </summary>
public interface ISagaState
{
    /// <summary>The correlation key. Unique per instance; the primary key in any real repository.</summary>
    string CorrelationId { get; set; }

    /// <summary>The state name the instance is currently in. Starts at <see cref="SagaStates.Initial"/>.</summary>
    string CurrentState { get; set; }

    /// <summary>
    /// Optimistic-concurrency token. Owned by the repository: it is <c>0</c> before the first insert and increments on
    /// every successful write. A write whose version no longer matches the stored one is rejected with
    /// <see cref="SagaConcurrencyException"/> rather than overwriting a concurrent transition.
    /// </summary>
    int Version { get; set; }

    /// <summary>When the instance reached <see cref="SagaStates.Final"/>; null while it is still running. Drives retention.</summary>
    DateTimeOffset? FinalizedAtUtc { get; set; }

    /// <summary>
    /// Live timeout tokens by timeout name. Written when a transition schedules a timeout and cleared when it fires or
    /// is cancelled; persisted with the rest of the state, which is what lets a cancellation survive a restart.
    /// </summary>
    IDictionary<string, string> TimeoutTokens { get; }

    /// <summary>
    /// An independent copy of this instance, of the same runtime type. A repository stores and returns copies, so a
    /// caller mutating the object it loaded cannot reach into the stored one and defeat the version check.
    /// </summary>
    ISagaState Copy();
}

/// <summary>
/// Convenience base for a saga state — implements every <see cref="ISagaState"/> member, so an application state adds
/// only its own business fields.
/// </summary>
/// <remarks>
/// <see cref="Copy"/> is a shallow copy (plus a fresh timeout dictionary): a nested mutable object is shared between
/// the copy and the original. Keep saga state flat — a saga row is a set of scalars and ids, and a flat shape is also
/// what a relational or document repository wants. Override <see cref="Copy"/> where a nested object is unavoidable.
/// </remarks>
public abstract class SagaState : ISagaState
{
    private Dictionary<string, string> _timeoutTokens = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string CorrelationId { get; set; } = string.Empty;

    /// <inheritdoc />
    public string CurrentState { get; set; } = SagaStates.Initial;

    /// <inheritdoc />
    public int Version { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? FinalizedAtUtc { get; set; }

    /// <inheritdoc />
    public IDictionary<string, string> TimeoutTokens => _timeoutTokens;

    /// <inheritdoc />
    public virtual ISagaState Copy()
    {
        var copy = (SagaState)MemberwiseClone();

        // The clone shares the original's dictionary reference until this line; without it, a stored copy and the
        // instance a handler is mutating would write into the same token map.
        copy._timeoutTokens = new Dictionary<string, string>(_timeoutTokens, StringComparer.Ordinal);
        return copy;
    }
}

/// <summary>
/// A saga write lost an optimistic-concurrency race: the stored instance changed between the load and the write, or a
/// second initiating event tried to create an instance that already exists.
/// </summary>
public sealed class SagaConcurrencyException : Exception
{
    /// <summary>Create the exception.</summary>
    public SagaConcurrencyException()
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The failure message.</param>
    public SagaConcurrencyException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying store failure, if any.</param>
    public SagaConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The correlation id of the instance that lost the race, when known.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The version the writer expected to find stored.</summary>
    public int ExpectedVersion { get; init; }

    /// <summary>Build an exception for a rejected write.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="expectedVersion">The version the writer expected to find stored.</param>
    public static SagaConcurrencyException For(string correlationId, int expectedVersion)
        => new(string.Create(CultureInfo.InvariantCulture, $"Saga instance '{correlationId}' changed concurrently; expected stored version {expectedVersion}."))
        {
            CorrelationId = correlationId,
            ExpectedVersion = expectedVersion,
        };
}

/// <summary>
/// Stores saga instances by correlation id. The default is in-memory; a durable implementation (EF, Mongo, Redis) maps
/// the same four operations onto its own store and is registered in its place.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
/// <para>
/// <b>Concurrency is optimistic and it is the implementation's job.</b> Two events for one instance can be processed at
/// the same time on different pump workers; without a version check the second write would silently discard the first
/// transition. So:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="InsertAsync"/> must fail with <see cref="SagaConcurrencyException"/> if the correlation id already exists — that is what makes a double-initiate race safe (unique key, not a version compare);</description></item>
///   <item><description><see cref="UpdateAsync"/> and <see cref="DeleteAsync"/> must fail with <see cref="SagaConcurrencyException"/> unless the stored <see cref="ISagaState.Version"/> still equals the one on the passed instance;</description></item>
///   <item><description>a successful write increments <see cref="ISagaState.Version"/> on both the stored copy and the passed instance.</description></item>
/// </list>
/// <para>
/// The coordinator answers a conflict by reloading and re-running the transition, so a rejected write costs a retry,
/// never a lost update.
/// </para>
/// </remarks>
public interface ISagaRepository<TState>
    where TState : class, ISagaState
{
    /// <summary>Load an instance, or null when none exists for the key.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>Store a new instance. Throws <see cref="SagaConcurrencyException"/> when the correlation id already exists.</summary>
    /// <param name="state">The new instance; its version moves 0 → 1 on success.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask InsertAsync(TState state, CancellationToken cancellationToken);

    /// <summary>Overwrite an instance. Throws <see cref="SagaConcurrencyException"/> when the stored version moved on.</summary>
    /// <param name="state">The instance as loaded and mutated; its version increments on success.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask UpdateAsync(TState state, CancellationToken cancellationToken);

    /// <summary>Remove a finalized instance. Throws <see cref="SagaConcurrencyException"/> when the stored version moved on.</summary>
    /// <param name="state">The instance to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeleteAsync(TState state, CancellationToken cancellationToken);

    /// <summary>
    /// Remove instances finalized longer ago than <paramref name="retention"/>; returns how many were removed. Only
    /// meaningful when <see cref="SagaOptions.RemoveOnFinalize"/> is off — that is the mode that keeps a finished
    /// instance around for inspection and needs a sweeper. No-op by default.
    /// </summary>
    /// <param name="retention">How long a finalized instance is kept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> PurgeFinalizedAsync(TimeSpan retention, CancellationToken cancellationToken) => ValueTask.FromResult(0);
}

/// <summary>Saga runtime behaviour — concurrency-conflict retries and what happens to an instance that finalizes.</summary>
public sealed class SagaOptions
{
    /// <summary>
    /// How many times a transition is re-run after losing an optimistic-concurrency race before the message is left to
    /// the normal retry / dead-letter path. Default 5.
    /// </summary>
    public int MaxConcurrencyRetries { get; set; } = 5;

    /// <summary>Pause between concurrency retries. Default 20ms — long enough for the winning writer to commit, short enough not to hold a consumer slot.</summary>
    public TimeSpan ConcurrencyRetryDelay { get; set; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Remove an instance the moment it reaches <see cref="SagaStates.Final"/>. Default true — a finished saga is not
    /// state, it is history, and history belongs in the log. Turn it off to keep finalized instances for inspection and
    /// sweep them later with <see cref="ISagaRepository{TState}.PurgeFinalizedAsync"/>.
    /// </summary>
    public bool RemoveOnFinalize { get; set; } = true;
}
