using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>Executes an <see cref="EventSagaDefinition"/>, compensating completed steps in reverse on failure.</summary>
public interface IEventSagaRunner
{
    /// <summary>Run a saga to completion or compensation.</summary>
    /// <param name="definition">The saga to run.</param>
    /// <param name="context">The shared context (carries the seed + accumulated results).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<EventSagaResult> RunAsync(EventSagaDefinition definition, EventSagaContext context, CancellationToken cancellationToken = default);
}
