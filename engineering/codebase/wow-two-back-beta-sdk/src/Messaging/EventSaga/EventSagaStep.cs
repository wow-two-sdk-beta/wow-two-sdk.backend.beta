using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>Convenience base for an <see cref="IEventSagaStep"/> — names itself after its type and compensates as a no-op.</summary>
public abstract class EventSagaStep : IEventSagaStep
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public abstract ValueTask<EventSagaStepOutcome> ExecuteAsync(EventSagaContext context, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask CompensateAsync(EventSagaContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
