using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>A load waiting for the write that will complete it — or for the end of the message, which is what makes it an ignored event.</summary>
internal interface ISagaPendingWrite
{
    /// <summary>Which pass over this instance produced it, from 1. Assigned by the bracket, which owns the counter.</summary>
    int Attempt { get; set; }

    /// <summary>Record the load as a non-write outcome.</summary>
    /// <param name="outcome">The outcome to record.</param>
    /// <param name="exception">The failure, when the message threw.</param>
    void Flush(SagaTransitionOutcome outcome, Exception? exception);
}
