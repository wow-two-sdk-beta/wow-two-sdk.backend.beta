namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Terminal moves — finalizing an instance from a clause, or from inside an activity.</summary>
public static class SagaFinalizationExtensions
{
    /// <summary>Move the instance to <see cref="SagaStateConstants.Final"/> when this clause runs — the saga is done.</summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="clause">The clause.</param>
    public static SagaEventClause<TState, TEvent> Finalize<TState, TEvent>(this SagaEventClause<TState, TEvent> clause)
        where TState : class, ISagaState
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(clause);
        clause.MarkFinalizing();
        return clause;
    }

    /// <summary>Finalize the instance from inside an activity — the conditional counterpart of the clause-level call.</summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="context">The transition context.</param>
    public static void Finalize<TState, TEvent>(this SagaTransitionContext<TState, TEvent> context)
        where TState : class, ISagaState
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        context.MarkFinalized();
    }
}
