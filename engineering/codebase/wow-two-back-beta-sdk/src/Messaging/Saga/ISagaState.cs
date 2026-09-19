using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Defines the persisted state of one saga instance. Implement it on a POCO (or derive from <see cref="SagaState"/>, which
/// implements every member) — the same object is what a repository stores.
/// </summary>
public interface ISagaState
{
    /// <summary>The correlation key. Unique per instance; the primary key in any real repository.</summary>
    string CorrelationId { get; set; }

    /// <summary>The state name the instance is currently in. Starts at <see cref="SagaStateConstants.Initial"/>.</summary>
    string CurrentState { get; set; }

    /// <summary>
    /// Optimistic-concurrency token. Owned by the repository: it is <c>0</c> before the first insert and increments on
    /// every successful write. A write whose version no longer matches the stored one is rejected with
    /// <see cref="SagaConcurrencyException"/> rather than overwriting a concurrent transition.
    /// </summary>
    int Version { get; set; }

    /// <summary>When the instance reached <see cref="SagaStateConstants.Final"/>; null while it is still running. Drives retention.</summary>
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
