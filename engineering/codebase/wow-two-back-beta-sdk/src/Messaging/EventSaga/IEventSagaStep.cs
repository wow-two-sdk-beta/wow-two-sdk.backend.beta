using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>
/// One step in a declarative event saga (routing slip). Steps run in order; each may write its result into the
/// shared <see cref="EventSagaContext"/> (the "result passed along"). On a downstream failure the runner walks the
/// completed steps in reverse calling <see cref="CompensateAsync"/> — automatic rollback.
/// </summary>
public interface IEventSagaStep
{
    /// <summary>Display name (used in logs and the Mermaid diagram).</summary>
    string Name { get; }

    /// <summary>Run the step. Return <see cref="EventSagaStepOutcome.Faulted"/> (or throw) to trigger compensation.</summary>
    /// <param name="context">The shared saga context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<EventSagaStepOutcome> ExecuteAsync(EventSagaContext context, CancellationToken cancellationToken);

    /// <summary>Undo this step's effect. Invoked in reverse order when a later step fails. No-op by default.</summary>
    /// <param name="context">The shared saga context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask CompensateAsync(EventSagaContext context, CancellationToken cancellationToken);
}
