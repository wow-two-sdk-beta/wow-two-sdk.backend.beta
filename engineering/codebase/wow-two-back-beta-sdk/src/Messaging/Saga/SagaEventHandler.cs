using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Adapts one observed event type onto the saga — an ordinary <see cref="IEventHandler{TEvent}"/>, so a saga consumes
/// through the same pump, filters, retry and dead-lettering as everything else, and shows up in the consumed-type set
/// the broker topology is built from.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event type.</typeparam>
internal sealed class SagaEventHandler<TState, TEvent>(SagaCoordinator<TState> coordinator, IServiceProvider services) : IEventHandler<TEvent>
    where TState : class, ISagaState, new()
    where TEvent : class, IEvent
{
    public ValueTask HandleAsync(EventContext<TEvent> context, CancellationToken cancellationToken)
        => coordinator.HandleAsync(context, services, cancellationToken);
}
