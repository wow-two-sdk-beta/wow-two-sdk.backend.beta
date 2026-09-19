using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// One message's worth of saga bookkeeping: the envelope being consumed, plus the loads that have not yet been paired
/// with a write. Ambient for the duration of the consume, so the repository — which the coordinator resolves from the
/// message scope — can see which event it is serving.
/// </summary>
internal sealed class SagaConsumeBracket(EventEnvelopeModel envelope)
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
    public EventEnvelopeModel Envelope => envelope;

    /// <summary>Register a load, stamping it with this instance's next attempt number.</summary>
    public void Pend((Type StateType, string CorrelationId) key, ISagaPendingWrite pending)
    {
        lock (_sync)
        {
            _attempts.TryGetValue(key, out var attempt);
            _attempts[key] = pending.Attempt = attempt + 1;

            // A delivery retry re-loads the same instance, so the newer load replaces its predecessor.
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
