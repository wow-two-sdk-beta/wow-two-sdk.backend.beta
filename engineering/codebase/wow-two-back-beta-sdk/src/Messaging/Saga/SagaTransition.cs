namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>One state's reaction to one event type: the activities to run and the edge to take.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
internal sealed class SagaTransition<TState, TEvent>
    where TState : class, ISagaState
    where TEvent : class, IEvent
{
    public required IReadOnlyList<Func<SagaTransitionContext<TState, TEvent>, CancellationToken, ValueTask>> Activities { get; init; }

    public string? TargetState { get; init; }

    public bool Finalizes { get; init; }
}
