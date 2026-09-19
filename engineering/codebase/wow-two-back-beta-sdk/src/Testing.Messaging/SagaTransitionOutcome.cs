using System.Collections.Concurrent;
using System.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>Refers to how one saga instance's reaction to one message ended.</summary>
public enum SagaTransitionOutcome
{
    /// <summary>The transition ran and its state was written — an insert, an update, or the delete that removes a finalized instance.</summary>
    Transitioned = 0,

    /// <summary>
    /// The write lost an optimistic-concurrency race. The coordinator answers by reloading and re-running, so a
    /// <see cref="Transitioned"/> record for the same instance follows at a higher
    /// <see cref="RecordedTransition{TState}.Attempt"/> — unless the retry budget ran out, in which case nothing does.
    /// </summary>
    Conflicted = 1,

    /// <summary>
    /// The instance was read and nothing was written: no instance existed and no clause initiates one, the current state
    /// has no clause for this event, or the message was a timeout the instance no longer expects.
    /// <see cref="RecordedTransition{TState}.FromState"/> tells the three apart — it is <c>null</c> only for the first.
    /// </summary>
    Ignored = 2,

    /// <summary>The message threw while the saga was handling it — <see cref="SagaMissingInstance.Fault"/>, an exhausted concurrency budget, or an activity that failed. One record per message, not per delivery attempt.</summary>
    Faulted = 3,
}
