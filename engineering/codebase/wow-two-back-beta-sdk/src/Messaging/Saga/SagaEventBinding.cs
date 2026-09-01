namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Type-erased binding for one observed event type — how to correlate it, and what it does per state.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal abstract class SagaEventBinding<TState>
    where TState : class, ISagaState
{
    public Func<object, EventEnvelope, string?>? Correlate { get; set; }

    public SagaMissingInstance MissingInstance { get; set; } = SagaMissingInstance.Ignore;

    public abstract Type EventType { get; }
}

/// <summary>Typed binding — keyed by source state, with <see cref="SagaStateConstants.Any"/> as the fallback key.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
internal sealed class SagaEventBinding<TState, TEvent> : SagaEventBinding<TState>
    where TState : class, ISagaState
    where TEvent : class, IEvent
{
    public override Type EventType => typeof(TEvent);

    public Dictionary<string, SagaTransition<TState, TEvent>> Transitions { get; } = new(StringComparer.Ordinal);
}
