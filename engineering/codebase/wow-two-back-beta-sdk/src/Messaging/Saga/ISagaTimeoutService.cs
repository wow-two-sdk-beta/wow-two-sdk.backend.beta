using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Sends a saga's timeout to itself, after the transition that asked for it has been written.</summary>
internal interface ISagaTimeoutService
{
    /// <summary>Deliver <paramref name="request"/>'s message back to the saga once its delay has elapsed.</summary>
    /// <param name="request">The timeout to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ScheduleAsync(SagaTimeoutRequest request, CancellationToken cancellationToken);
}
