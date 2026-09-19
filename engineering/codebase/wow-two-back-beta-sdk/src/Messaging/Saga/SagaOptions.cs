using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Holds saga runtime behaviour — concurrency-conflict retries and what happens to an instance that finalizes.</summary>
public sealed record SagaOptions
{
    /// <summary>
    /// How many times a transition is re-run after losing an optimistic-concurrency race before the message is left to
    /// the normal retry / dead-letter path. Default 5.
    /// </summary>
    public int MaxConcurrencyRetries { get; set; } = 5;

    /// <summary>Pause between concurrency retries. Default 20ms — long enough for the winning writer to commit, short enough not to hold a consumer slot.</summary>
    public TimeSpan ConcurrencyRetryDelay { get; set; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Remove an instance the moment it reaches <see cref="SagaStateConstants.Final"/>. Default true — a finished saga is not
    /// state, it is history, and history belongs in the log. Turn it off to keep finalized instances for inspection and
    /// sweep them later with <see cref="ISagaRepository{TState}.PurgeFinalizedAsync"/>.
    /// </summary>
    public bool RemoveOnFinalize { get; set; } = true;
}
